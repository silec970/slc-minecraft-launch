using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Domain.Abstractions;

public interface IJavaRuntimeService
{
    Task<JavaRuntimeProvisionOutcome> ProvisionMajorAsync(
        int majorVersion,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<JavaRuntimeResolution> EnsureAsync(
        string versionId,
        LoaderKind loaderKind,
        string? explicitJavaPath = null,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

