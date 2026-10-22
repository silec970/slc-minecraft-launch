using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Domain.Abstractions;

public interface ILoaderInstallationService
{
    Task<LoaderInstallOutcome> InstallAsync(
        LoaderInstallRequest request,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
