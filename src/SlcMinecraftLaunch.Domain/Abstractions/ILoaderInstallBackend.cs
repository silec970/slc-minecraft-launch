using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Domain.Abstractions;

public interface ILoaderInstallBackend
{
    Task<LoaderInstallOutcome> InstallAsync(
        LoaderInstallPlan plan,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

