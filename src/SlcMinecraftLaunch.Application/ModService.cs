using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Application;

public sealed class ModService(
    IGameRuntimeProvider runtimeProvider,
    IModProviderCatalog providerCatalog,
    ILocalModStore localModStore,
    IVersionPreferencesStore preferencesStore,
    ModConflictAnalyzer conflictAnalyzer) : IModService
{
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
        var mods = await ScanAsync(versionId, cancellationToken).ConfigureAwait(false);
        return conflictAnalyzer.Analyze(mods);
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
                "Mods.Installed",
                installed.FilePath,
                Conflicts: conflictAnalyzer.Analyze(mods));
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
        var mods = await ScanAsync(request.MinecraftVersion, cancellationToken).ConfigureAwait(false);
        return new ModInstallOutcome(
            true,
            "Mods.Installed",
            installedPath,
            Conflicts: conflictAnalyzer.Analyze(mods));
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
}
