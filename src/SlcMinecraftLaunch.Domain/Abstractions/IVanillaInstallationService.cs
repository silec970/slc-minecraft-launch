using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Domain.Abstractions;

public interface IVanillaInstallationService
{
    Task<VanillaInstallationOutcome> InstallAsync(
        string versionId,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
