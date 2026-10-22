using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Domain.Abstractions;

public interface IJavaRuntimeService
{
    Task<JavaRuntimeResolution> EnsureAsync(
        string versionId,
        LoaderKind loaderKind,
        string? explicitJavaPath = null,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

