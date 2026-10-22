using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Application;

public sealed class InstanceTransferService(
    IGameRuntimeProvider runtimeProvider,
    IInstanceStorage instanceStorage,
    IVersionPreferencesStore preferencesStore,
    IModSourceStore modSourceStore) : IInstanceTransferService
{
    private readonly SemaphoreSlim _transferLock = new(1, 1);

    public async Task<InstanceOperationOutcome> ExportAsync(
        string instanceId,
        string destinationPath,
        InstanceExportOptions options,
        CancellationToken cancellationToken = default)
    {
        var (manifest, isolation) = await ResolveContextAsync(instanceId, cancellationToken)
            .ConfigureAwait(false);
        if (manifest is null)
        {
            return new InstanceOperationOutcome(false, "Instances.NotFound", instanceId);
        }

        try
        {
            await instanceStorage.ExportAsync(
                manifest,
                isolation,
                options,
                destinationPath,
                cancellationToken).ConfigureAwait(false);
            return new InstanceOperationOutcome(true, "Instances.Exported", instanceId);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new InstanceOperationOutcome(
                false,
                "Instances.ExportFailed",
                instanceId,
                exception.Message);
        }
    }

    public Task<InstanceOperationOutcome> SaveBackupAsync(
        string instanceId,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        return ExportAsync(
            instanceId,
            destinationPath,
            new InstanceExportOptions(
                IncludeSaves: true,
                IncludeScreenshots: true,
                IncludeServersFile: true,
                IncludeLogs: true,
                IncludeDefaultGameplayData: true),
            cancellationToken);
    }

    public async Task<InstanceOperationOutcome> ImportAsync(
        string archivePath,
        CancellationToken cancellationToken = default)
    {
        await _transferLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ImportedInstanceArchive archive;
            try
            {
                archive = await instanceStorage
                    .InspectArchiveAsync(archivePath, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                return new InstanceOperationOutcome(
                    false,
                    "Instances.ImportInvalid",
                    ErrorDetail: exception.Message);
            }

            var targetId = archive.Manifest.Id;
            if (await instanceStorage.VersionExistsAsync(targetId, cancellationToken).ConfigureAwait(false))
            {
                return new InstanceOperationOutcome(false, "Instances.AlreadyExists", targetId);
            }

            var runtime = await runtimeProvider.GetAsync(cancellationToken).ConfigureAwait(false);
            var versions = await runtime.GetVersionsAsync(cancellationToken).ConfigureAwait(false);
            var matchingVersion = versions.FirstOrDefault(version =>
                string.Equals(
                    version.BaseVersion,
                    archive.Manifest.MinecraftVersion,
                    StringComparison.OrdinalIgnoreCase) &&
                version.LoaderKind == archive.Manifest.LoaderKind &&
                string.Equals(
                    version.LoaderVersion,
                    archive.Manifest.LoaderVersion,
                    StringComparison.OrdinalIgnoreCase));
            if (matchingVersion is null && !archive.Manifest.IsPlaceholder)
            {
                return new InstanceOperationOutcome(
                    false,
                    "Instances.ImportVersionMissing",
                    targetId,
                    $"{archive.Manifest.MinecraftVersion}/{archive.Manifest.LoaderKind}/{archive.Manifest.LoaderVersion}");
            }

            var createdTarget = false;
            try
            {
                if (archive.Manifest.IsPlaceholder)
                {
                    await instanceStorage
                        .CreatePlaceholderAsync(archive.Manifest, cancellationToken)
                        .ConfigureAwait(false);
                }
                else
                {
                    await instanceStorage.CopyVersionAsync(
                        matchingVersion!.Id,
                        targetId,
                        cancellationToken).ConfigureAwait(false);
                }

                createdTarget = true;
                await instanceStorage
                    .ExtractArchiveDataAsync(archive, true, cancellationToken)
                    .ConfigureAwait(false);
                await MergeModSourcesAsync(archive.ModSources, cancellationToken).ConfigureAwait(false);
                await SetIsolationPreferenceAsync(targetId, cancellationToken).ConfigureAwait(false);
                return new InstanceOperationOutcome(true, "Instances.Imported", targetId);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                if (createdTarget)
                {
                    try
                    {
                        await instanceStorage
                            .DeleteInstanceAsync(targetId, CancellationToken.None)
                            .ConfigureAwait(false);
                    }
                    catch
                    {
                        // The original import error remains the primary failure.
                    }
                }

                return new InstanceOperationOutcome(
                    false,
                    "Instances.ImportFailed",
                    targetId,
                    exception.Message);
            }
        }
        finally
        {
            _transferLock.Release();
        }
    }

    private async Task<(InstanceManifest? Manifest, bool Isolation)> ResolveContextAsync(
        string instanceId,
        CancellationToken cancellationToken)
    {
        var runtime = await runtimeProvider.GetAsync(cancellationToken).ConfigureAwait(false);
        var versions = await runtime.GetVersionsAsync(cancellationToken).ConfigureAwait(false);
        var version = versions.FirstOrDefault(item =>
            string.Equals(item.Id, instanceId, StringComparison.OrdinalIgnoreCase));
        if (version is null)
        {
            return (null, true);
        }

        var manifest = await instanceStorage
                           .LoadManifestAsync(instanceId, cancellationToken)
                           .ConfigureAwait(false)
                       ?? InstanceManifestFactory.FromVersion(version);
        var preferences = await preferencesStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        return (
            manifest,
            (preferences.GetValueOrDefault(instanceId) ?? new VersionPreference()).IsIsolated);
    }

    private async Task MergeModSourcesAsync(
        IReadOnlyDictionary<string, ModSourceMetadata> importedSources,
        CancellationToken cancellationToken)
    {
        if (importedSources.Count == 0)
        {
            return;
        }

        var sources = new Dictionary<string, ModSourceMetadata>(
            await modSourceStore.LoadAsync(cancellationToken).ConfigureAwait(false),
            StringComparer.OrdinalIgnoreCase);
        foreach (var source in importedSources)
        {
            sources[source.Key] = source.Value;
        }

        await modSourceStore.SaveAsync(sources, cancellationToken).ConfigureAwait(false);
    }

    private async Task SetIsolationPreferenceAsync(
        string instanceId,
        CancellationToken cancellationToken)
    {
        var preferences = new Dictionary<string, VersionPreference>(
            await preferencesStore.LoadAsync(cancellationToken).ConfigureAwait(false),
            StringComparer.OrdinalIgnoreCase);
        var current = preferences.GetValueOrDefault(instanceId) ?? new VersionPreference();
        preferences[instanceId] = current with { IsIsolated = true };
        await preferencesStore.SaveAsync(preferences, cancellationToken).ConfigureAwait(false);
    }
}
