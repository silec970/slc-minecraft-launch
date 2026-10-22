using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Application;

public sealed class LoaderInstallationService(
    ILoaderCatalogService loaderCatalog,
    IJavaRuntimeService javaRuntimeService,
    ILoaderInstallBackend loaderInstallBackend,
    IGameRuntimeProvider runtimeProvider) : ILoaderInstallationService
{
    public async Task<LoaderInstallOutcome> InstallAsync(
        LoaderInstallRequest request,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (request.LoaderKind is LoaderKind.Vanilla or LoaderKind.Other)
        {
            return new LoaderInstallOutcome(
                false,
                "Loader.UnsupportedKind");
        }

        progress?.Report(new OperationProgress(
            SmokeTestStage.InstallingLoader,
            "Progress.ResolvingLoaderVersion",
            0.05));

        var loaderVersions = await loaderCatalog.GetVersionsAsync(
            request.MinecraftVersion,
            request.LoaderKind,
            cancellationToken).ConfigureAwait(false);

        if (loaderVersions.Count == 0)
        {
            return new LoaderInstallOutcome(
                false,
                "Loader.NoVersions",
                ErrorDetail: request.MinecraftVersion);
        }

        var loaderVersion = string.IsNullOrWhiteSpace(request.LoaderVersion)
            ? loaderVersions.FirstOrDefault(version => version.IsStable)?.Version
              ?? loaderVersions[0].Version
            : loaderVersions.FirstOrDefault(version =>
                  string.Equals(version.Version, request.LoaderVersion, StringComparison.OrdinalIgnoreCase))
              ?.Version;

        if (loaderVersion is null)
        {
            return new LoaderInstallOutcome(
                false,
                "Loader.VersionNotFound",
                ErrorDetail: request.LoaderVersion);
        }

        var java = await javaRuntimeService.EnsureAsync(
            request.MinecraftVersion,
            request.LoaderKind,
            request.JavaPath,
            progress,
            cancellationToken).ConfigureAwait(false);

        if (java.Selected is null)
        {
            return new LoaderInstallOutcome(
                false,
                java.MessageKey,
                ErrorDetail: java.ErrorDetail);
        }

        var customVersionId = string.IsNullOrWhiteSpace(request.CustomVersionId)
            ? $"{request.MinecraftVersion}-{request.LoaderKind.ToString().ToLowerInvariant()}-{loaderVersion}"
            : request.CustomVersionId;

        progress?.Report(new OperationProgress(
            SmokeTestStage.InstallingLoader,
            "Progress.InstallingLoader",
            0.3));

        var result = await loaderInstallBackend.InstallAsync(
            new LoaderInstallPlan(
                request.MinecraftVersion,
                request.LoaderKind,
                loaderVersion,
                java.Selected.ExecutablePath,
                customVersionId),
            progress,
            cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            return result with
            {
                Warnings = java.Warnings.Select(warning => $"Java.{warning}").ToArray()
            };
        }

        var runtime = await runtimeProvider.GetAsync(cancellationToken).ConfigureAwait(false);
        var versions = await runtime.GetVersionsAsync(cancellationToken).ConfigureAwait(false);
        if (!versions.Any(version =>
                string.Equals(version.Id, result.InstalledVersionId, StringComparison.OrdinalIgnoreCase)))
        {
            return new LoaderInstallOutcome(
                false,
                "Loader.VersionNotResolvable",
                result.InstalledVersionId,
                "The locator couldn't resolve the installed loader version.",
                java.Warnings.Select(warning => $"Java.{warning}").ToArray());
        }

        progress?.Report(new OperationProgress(
            SmokeTestStage.Completed,
            "Loader.Installed",
            1,
            Detail: result.InstalledVersionId));

        return result with
        {
            Warnings = java.Warnings.Select(warning => $"Java.{warning}").ToArray()
        };
    }
}
