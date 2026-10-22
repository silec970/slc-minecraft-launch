using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Application;

public sealed class VanillaInstallationService(
    IGameVersionMaterializer versionMaterializer,
    IGameRuntimeProvider runtimeProvider,
    IJavaRuntimeService javaRuntimeService,
    IDownloadService downloadService) : IVanillaInstallationService
{
    public async Task<VanillaInstallationOutcome> InstallAsync(
        string versionId,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(versionId))
        {
            return new VanillaInstallationOutcome(
                false,
                "Download.VersionRequired",
                string.Empty);
        }

        var runtime = await runtimeProvider.GetAsync(cancellationToken).ConfigureAwait(false);
        progress?.Report(new OperationProgress(
            SmokeTestStage.MaterializingVersion,
            "Download.MaterializingVanilla",
            0.02,
            Detail: versionId));

        await versionMaterializer.EnsureVersionAsync(
            runtime.RootPath,
            versionId,
            progress,
            cancellationToken).ConfigureAwait(false);

        var versions = await runtime.GetVersionsAsync(cancellationToken).ConfigureAwait(false);
        var version = versions.FirstOrDefault(item =>
            string.Equals(item.Id, versionId, StringComparison.OrdinalIgnoreCase));
        if (version is null || version.IsBroken)
        {
            return new VanillaInstallationOutcome(
                false,
                "Download.VersionNotResolved",
                versionId);
        }

        progress?.Report(new OperationProgress(
            SmokeTestStage.DetectingJava,
            "Download.PreparingJava",
            0.12));
        var java = await javaRuntimeService.EnsureAsync(
            version.Id,
            LoaderKind.Vanilla,
            progress: progress,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (java.Selected is null)
        {
            return new VanillaInstallationOutcome(
                false,
                java.MessageKey,
                versionId,
                java.ErrorDetail);
        }

        progress?.Report(new OperationProgress(
            SmokeTestStage.CompletingResources,
            "Download.CompletingVanilla",
            0.25));
        var resources = await downloadService.CompleteResourcesAsync(
            new ResourceCompletionRequest(version.Id, java.Selected.ExecutablePath),
            progress,
            cancellationToken).ConfigureAwait(false);
        if (!resources.Succeeded)
        {
            return new VanillaInstallationOutcome(
                false,
                resources.MessageKey,
                versionId,
                resources.ErrorDetail);
        }

        progress?.Report(new OperationProgress(
            SmokeTestStage.Completed,
            "Download.VanillaInstalled",
            1,
            Detail: versionId));
        return new VanillaInstallationOutcome(
            true,
            "Download.VanillaInstalled",
            versionId);
    }
}
