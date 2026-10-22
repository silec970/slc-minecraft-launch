using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Application;

public sealed class JavaRuntimeService(
    IJavaDiscovery javaDiscovery,
    IJavaRuntimeProvisioner javaRuntimeProvisioner,
    IGameRuntimeProvider runtimeProvider,
    JavaSelectionPolicy javaSelectionPolicy) : IJavaRuntimeService
{
    public async Task<JavaRuntimeResolution> EnsureAsync(
        string versionId,
        LoaderKind loaderKind,
        string? explicitJavaPath = null,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var runtime = await runtimeProvider.GetAsync(cancellationToken).ConfigureAwait(false);
        var versions = await runtime.GetVersionsAsync(cancellationToken).ConfigureAwait(false);
        var version = versions.FirstOrDefault(item =>
            string.Equals(item.Id, versionId, StringComparison.OrdinalIgnoreCase));

        if (version is null)
        {
            return new JavaRuntimeResolution(
                null,
                Array.Empty<JavaSelectionWarning>(),
                false,
                "Java.VersionNotFound",
                versionId);
        }

        progress?.Report(new OperationProgress(
            SmokeTestStage.DetectingJava,
            "Progress.DetectingJava",
            0.1));

        var candidates = await javaDiscovery.DiscoverAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(explicitJavaPath))
        {
            var explicitCandidate = await javaDiscovery
                .InspectAsync(explicitJavaPath, cancellationToken)
                .ConfigureAwait(false);

            if (explicitCandidate is null)
            {
                return new JavaRuntimeResolution(
                    null,
                    Array.Empty<JavaSelectionWarning>(),
                    false,
                    "Java.ExplicitUnavailable",
                    explicitJavaPath);
            }

            candidates = candidates
                .Append(explicitCandidate)
                .DistinctBy(candidate => candidate.ExecutablePath, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        var selected = javaSelectionPolicy.Select(
            candidates,
            version.BaseVersion,
            loaderKind,
            explicitJavaPath);

        if (selected.Selected is not null || !string.IsNullOrWhiteSpace(explicitJavaPath))
        {
            progress?.Report(new OperationProgress(
                SmokeTestStage.SelectingJava,
                "Progress.JavaSelected",
                0.25,
                Detail: selected.Selected?.ExecutablePath));
            return new JavaRuntimeResolution(
                selected.Selected,
                selected.Warnings,
                false,
                selected.Selected is null ? "Java.NoCompatibleRuntime" : "Java.Ready");
        }

        var requiredMajor = JavaSelectionPolicy.GetRequiredJavaMajor(version.BaseVersion);
        progress?.Report(new OperationProgress(
            SmokeTestStage.DownloadingJava,
            "Progress.DownloadingJava",
            0.15));

        var provisioned = await javaRuntimeProvisioner.EnsureAsync(
            requiredMajor,
            version.JavaComponent,
            progress,
            cancellationToken).ConfigureAwait(false);

        if (!provisioned.Succeeded || string.IsNullOrWhiteSpace(provisioned.ExecutablePath))
        {
            return new JavaRuntimeResolution(
                null,
                Array.Empty<JavaSelectionWarning>(),
                false,
                provisioned.MessageKey,
                provisioned.ErrorDetail);
        }

        var provisionedCandidate = await javaDiscovery
            .InspectAsync(provisioned.ExecutablePath, cancellationToken)
            .ConfigureAwait(false);
        if (provisionedCandidate is null)
        {
            return new JavaRuntimeResolution(
                null,
                Array.Empty<JavaSelectionWarning>(),
                true,
                "Java.ProvisionedRuntimeUnreadable",
                provisioned.ExecutablePath);
        }

        var provisionedSelection = javaSelectionPolicy.Select(
            [provisionedCandidate],
            version.BaseVersion,
            loaderKind);
        progress?.Report(new OperationProgress(
            SmokeTestStage.SelectingJava,
            "Progress.JavaSelected",
            0.25,
            Detail: provisionedCandidate.ExecutablePath));

        return new JavaRuntimeResolution(
            provisionedSelection.Selected,
            provisionedSelection.Warnings,
            true,
            provisionedSelection.Selected is null ? "Java.NoCompatibleRuntime" : "Java.Ready");
    }
}
