using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Domain.Abstractions;

public interface IJavaRuntimeProvisioner
{
    Task<JavaRuntimeProvisionOutcome> EnsureAsync(
        int requiredMajorVersion,
        string? mojangComponent,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

