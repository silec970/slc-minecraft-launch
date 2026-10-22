using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Application;

public sealed class ModService(
    IGameRuntimeProvider runtimeProvider,
    IModProviderCatalog providerCatalog,
    ILocalModStore localModStore,
    IModSourceStore modSourceStore,
    IVersionPreferencesStore preferencesStore,
    ModConflictAnalyzer conflictAnalyzer) : IModService
{
    private readonly SemaphoreSlim _sourceWriteLock = new(1, 1);

    public Task<ModSearchOutcome> SearchAsync(
        ModSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        return providerCatalog.SearchAsync(query, cancellationToken);
    }

    public async Task<IReadOnlyList<ModEntry>> ScanAsync(
        string versionId,
        CancellationToken cancellationToken = default)
    {
        var context = await GetContextAsync(versionId, cancellationToken).ConfigureAwait(false);
        return await localModStore.ScanAsync(
            context.Runtime.RootPath,
            versionId,
            context.VersionIsolation,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ModConflict>> DetectConflictsAsync(
        string versionId,
        CancellationToken cancellationToken = default)
    {
        var context = await GetContextAsync(versionId, cancellationToken).ConfigureAwait(false);
        var versions = await context.Runtime.GetVersionsAsync(cancellationToken).ConfigureAwait(false);
        var loader = versions.FirstOrDefault(version =>
            string.Equals(version.Id, versionId, StringComparison.OrdinalIgnoreCase))?.LoaderKind;
        var mods = await localModStore.ScanAsync(
            context.Runtime.RootPath,
            versionId,
            context.VersionIsolation,
            cancellationToken).ConfigureAwait(false);
        var conflicts = conflictAnalyzer.Analyze(mods, loader);
        var sources = await modSourceStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        return conflicts.Select(conflict => EnrichConflict(conflict, sources)).ToArray();
    }

    public async Task<ModInstallOutcome> InstallLocalAsync(
        LocalModInstallRequest request,
        CancellationToken cancellationToken = default)
    {
        var context = await GetContextAsync(
            request.MinecraftVersion,
            cancellationToken).ConfigureAwait(false);

        try
        {
            var installed = await localModStore.ImportAsync(
                context.Runtime.RootPath,
                request.MinecraftVersion,
                context.VersionIsolation,
                request.SourceFilePath,
                cancellationToken).ConfigureAwait(false);
            var mods = await ScanAsync(request.MinecraftVersion, cancellationToken).ConfigureAwait(false);

            return new ModInstallOutcome(
                true,
                "Mods.InstalledSuccess",
                installed.FilePath,
                Conflicts: conflictAnalyzer.Analyze(mods, request.LoaderKind));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new ModInstallOutcome(
                false,
                "Mods.InvalidPackage",
                ErrorDetail: exception.Message);
        }
    }

    public async Task<ModInstallOutcome> InstallRemoteAsync(
        ModInstallRequest request,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var download = await providerCatalog.ResolveDownloadAsync(
            request,
            cancellationToken).ConfigureAwait(false);
        if (download is null)
        {
            return new ModInstallOutcome(
                false,
                "Mods.NoCompatibleFile");
        }

        var context = await GetContextAsync(
            request.MinecraftVersion,
            cancellationToken).ConfigureAwait(false);
        var modsDirectory = localModStore.GetModsDirectory(
            context.Runtime.RootPath,
            request.MinecraftVersion,
            context.VersionIsolation);

        progress?.Report(new OperationProgress(
            SmokeTestStage.DownloadingMod,
            "Mods.Downloading",
            0.1,
            Detail: download.FileName));

        var completion = await context.Runtime.DownloadModAsync(
            new ModDownloadRequest(
                request.MinecraftVersion,
                modsDirectory,
                download.DownloadUrl,
                download.FileName,
                download.Sha1,
                download.Size),
            progress,
            cancellationToken).ConfigureAwait(false);
        if (!completion.Succeeded)
        {
            return new ModInstallOutcome(
                false,
                completion.MessageKey,
                ErrorDetail: completion.ErrorDetail);
        }

        var installedPath = Path.Combine(modsDirectory, download.FileName);
        var warnings = string.IsNullOrWhiteSpace(download.Sha1)
            ? new[] { "Mods.HashUnavailable" }
            : Array.Empty<string>();
        var inspected = await localModStore
            .InspectAsync(installedPath, cancellationToken)
            .ConfigureAwait(false);
        if (inspected is null)
        {
            await Task.Run(() => File.Delete(installedPath), cancellationToken).ConfigureAwait(false);
            return new ModInstallOutcome(
                false,
                "Mods.InvalidPackage",
                installedPath,
                "Downloaded file is not a valid supported mod JAR.",
                Warnings: warnings);
        }

        await SaveSourceAsync(
            inspected.Metadata.ModId,
            new ModSourceMetadata(
                inspected.Metadata.ModId,
                download.Provider,
                download.ProjectId,
                download.VersionId,
                download.Dependencies ?? Array.Empty<ModDependencySource>()),
            cancellationToken).ConfigureAwait(false);

        var mods = await ScanAsync(request.MinecraftVersion, cancellationToken).ConfigureAwait(false);
        return new ModInstallOutcome(
            true,
            "Mods.InstalledSuccess",
            installedPath,
            Conflicts: conflictAnalyzer.Analyze(mods, request.LoaderKind),
            Warnings: warnings);
    }

    public async Task<ModInstallOutcome> InstallRecommendedDependencyAsync(
        string versionId,
        LoaderKind loaderKind,
        ModConflict conflict,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (conflict.Provider is null || string.IsNullOrWhiteSpace(conflict.ProjectId))
        {
            return new ModInstallOutcome(
                false,
                "Mods.CannotResolveDependency",
                ErrorDetail: conflict.SecondModId);
        }

        return await InstallRemoteAsync(
            new ModInstallRequest(
                conflict.Provider.Value,
                conflict.ProjectId,
                versionId,
                loaderKind),
            progress,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<(IGameRuntime Runtime, bool VersionIsolation)> GetContextAsync(
        string versionId,
        CancellationToken cancellationToken)
    {
        var runtime = await runtimeProvider.GetAsync(cancellationToken).ConfigureAwait(false);
        var preferences = await preferencesStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        return (
            runtime,
            (preferences.GetValueOrDefault(versionId) ?? new VersionPreference()).IsIsolated);
    }

    private async Task SaveSourceAsync(
        string modId,
        ModSourceMetadata source,
        CancellationToken cancellationToken)
    {
        await _sourceWriteLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var sources = new Dictionary<string, ModSourceMetadata>(
                await modSourceStore.LoadAsync(cancellationToken).ConfigureAwait(false),
                StringComparer.OrdinalIgnoreCase)
            {
                [modId] = source
            };
            await modSourceStore.SaveAsync(sources, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _sourceWriteLock.Release();
        }
    }

    private static ModConflict EnrichConflict(
        ModConflict conflict,
        IReadOnlyDictionary<string, ModSourceMetadata> sources)
    {
        if (conflict.Provider is not null ||
            string.IsNullOrWhiteSpace(conflict.FirstModId) ||
            string.IsNullOrWhiteSpace(conflict.SecondModId) ||
            !sources.TryGetValue(conflict.FirstModId, out var parentSource))
        {
            return conflict;
        }

        var dependency = parentSource.Dependencies.FirstOrDefault(item =>
            string.Equals(item.ModId, conflict.SecondModId, StringComparison.OrdinalIgnoreCase));
        return dependency is null
            ? conflict
            : conflict with
            {
                Provider = dependency.Provider,
                ProjectId = dependency.ProjectId,
                RequiredVersionId = dependency.VersionId
            };
    }
}
