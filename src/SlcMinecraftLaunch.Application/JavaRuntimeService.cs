using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Application;

public sealed class JavaRuntimeService(
    IJavaDiscovery javaDiscovery,
    IJavaRuntimeStore javaRuntimeStore,
    IJavaRuntimeProvisioner javaRuntimeProvisioner,
    IGameRuntimeProvider runtimeProvider,
    JavaSelectionPolicy javaSelectionPolicy,
    IJavaSettingsStore javaSettingsStore) : IJavaRuntimeManagementService
{
    public async Task<JavaRuntimeProvisionOutcome> ProvisionMajorAsync(
        int majorVersion,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var runtime = await runtimeProvider.GetAsync(cancellationToken).ConfigureAwait(false);
        var versions = await runtime.GetVersionsAsync(cancellationToken).ConfigureAwait(false);
        var component = versions
            .Where(version => JavaSelectionPolicy.GetRequiredJavaMajor(version.BaseVersion) == majorVersion)
            .Select(version => version.JavaComponent)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        return await javaRuntimeProvisioner.EnsureAsync(
            majorVersion,
            component,
            progress,
            cancellationToken).ConfigureAwait(false);
    }

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
        var manualJavaPath = await javaSettingsStore
            .LoadAsync(cancellationToken)
            .ConfigureAwait(false);
        var effectiveJavaPath = !string.IsNullOrWhiteSpace(explicitJavaPath)
            ? explicitJavaPath
            : manualJavaPath.ManualExecutablePath;
        if (!string.IsNullOrWhiteSpace(effectiveJavaPath))
        {
            var explicitCandidate = await javaDiscovery
                .InspectAsync(effectiveJavaPath, cancellationToken)
                .ConfigureAwait(false);

            if (explicitCandidate is null)
            {
                return new JavaRuntimeResolution(
                    null,
                    Array.Empty<JavaSelectionWarning>(),
                    false,
                    string.IsNullOrWhiteSpace(explicitJavaPath)
                        ? "Java.ManualPathUnavailable"
                        : "Java.ExplicitUnavailable",
                    effectiveJavaPath);
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
            effectiveJavaPath);

        if (selected.Selected is not null || !string.IsNullOrWhiteSpace(effectiveJavaPath))
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

    public async Task<IReadOnlyList<JavaRuntimeDescriptor>> GetRuntimesAsync(
        CancellationToken cancellationToken = default)
    {
        var candidates = await javaDiscovery.DiscoverAsync(cancellationToken).ConfigureAwait(false);
        var manualPath = await javaSettingsStore
            .LoadAsync(cancellationToken)
            .ConfigureAwait(false);

        return candidates
            .Select(candidate => new JavaRuntimeDescriptor(
                candidate,
                JavaSelectionPolicy.GetCompatibilityKey(candidate.MajorVersion),
                GetSourceKey(candidate.Source),
                GetIntegrityKey(candidate.IntegrityStatus),
                !string.IsNullOrWhiteSpace(manualPath.ManualExecutablePath) &&
                PathsEqual(candidate.ExecutablePath, manualPath.ManualExecutablePath)))
            .OrderByDescending(runtime => runtime.IsManualOverride)
            .ThenBy(runtime => runtime.MajorVersion)
            .ThenBy(runtime => runtime.ExecutablePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<JavaRuntimeManagementOutcome> ReverifyAsync(
        string executablePath,
        CancellationToken cancellationToken = default)
    {
        var candidate = await javaRuntimeStore
            .ReverifyAsync(executablePath, cancellationToken)
            .ConfigureAwait(false);
        if (candidate is null)
        {
            return new JavaRuntimeManagementOutcome(
                false,
                "Java.ReverifyNotAllowed",
                executablePath);
        }

        return candidate.IntegrityStatus == JavaIntegrityStatus.Verified
            ? new JavaRuntimeManagementOutcome(true, "Java.RuntimeVerified")
            : new JavaRuntimeManagementOutcome(
                false,
                "Java.RuntimeInvalid",
                candidate.IntegrityDetail);
    }

    public async Task<JavaRuntimeManagementOutcome> DeleteManagedAsync(
        string executablePath,
        CancellationToken cancellationToken = default)
    {
        var deleted = await javaRuntimeStore
            .DeleteManagedAsync(executablePath, cancellationToken)
            .ConfigureAwait(false);
        return deleted
            ? new JavaRuntimeManagementOutcome(true, "Java.ManagedRuntimeDeleted")
            : new JavaRuntimeManagementOutcome(
                false,
                "Java.DeleteNotAllowed",
                executablePath);
    }

    public async Task<string?> GetManualExecutablePathAsync(
        CancellationToken cancellationToken = default)
    {
        var settings = await javaSettingsStore
            .LoadAsync(cancellationToken)
            .ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(settings.ManualExecutablePath)
            ? null
            : settings.ManualExecutablePath;
    }

    public async Task<JavaRuntimeManagementOutcome> SaveManualExecutablePathAsync(
        string? executablePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            await javaSettingsStore
                .SaveAsync(new JavaSettings(), cancellationToken)
                .ConfigureAwait(false);
            return new JavaRuntimeManagementOutcome(true, "Java.ManualPathCleared");
        }

        var candidate = await javaDiscovery
            .InspectAsync(executablePath, cancellationToken)
            .ConfigureAwait(false);
        if (candidate is null)
        {
            return new JavaRuntimeManagementOutcome(
                false,
                "Java.ManualPathInvalid",
                executablePath);
        }

        var fullPath = Path.GetFullPath(candidate.ExecutablePath);
        await javaSettingsStore
            .SaveAsync(new JavaSettings(fullPath), cancellationToken)
            .ConfigureAwait(false);
        return new JavaRuntimeManagementOutcome(true, "Java.ManualPathSaved");
    }

    private static string GetSourceKey(JavaRuntimeSource source)
    {
        return source switch
        {
            JavaRuntimeSource.Mojang => "Java.Source.Mojang",
            JavaRuntimeSource.Adoptium => "Java.Source.Adoptium",
            _ => "Java.Source.System"
        };
    }

    private static string GetIntegrityKey(JavaIntegrityStatus status)
    {
        return status switch
        {
            JavaIntegrityStatus.Recorded => "Java.Integrity.Recorded",
            JavaIntegrityStatus.Verified => "Java.Integrity.Verified",
            JavaIntegrityStatus.Invalid => "Java.Integrity.Invalid",
            JavaIntegrityStatus.Unknown => "Java.Integrity.Unknown",
            _ => "Java.Integrity.NotManaged"
        };
    }

    private static bool PathsEqual(string left, string right)
    {
        try
        {
            return string.Equals(
                Path.GetFullPath(left),
                Path.GetFullPath(right),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is ArgumentException
                                               or NotSupportedException
                                               or PathTooLongException)
        {
            return false;
        }
    }
}
