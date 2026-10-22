using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Domain.Abstractions;

public interface IModService
{
    Task<ModSearchOutcome> SearchAsync(
        ModSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ModEntry>> ScanAsync(
        string versionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ModConflict>> DetectConflictsAsync(
        string versionId,
        CancellationToken cancellationToken = default);

    Task<ModInstallOutcome> InstallLocalAsync(
        LocalModInstallRequest request,
        CancellationToken cancellationToken = default);

    Task<ModInstallOutcome> InstallRemoteAsync(
        ModInstallRequest request,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

