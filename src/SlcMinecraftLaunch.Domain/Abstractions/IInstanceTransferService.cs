using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Domain.Abstractions;

public interface IInstanceTransferService
{
    Task<InstanceOperationOutcome> ExportAsync(
        string instanceId,
        string destinationPath,
        InstanceExportOptions options,
        CancellationToken cancellationToken = default);

    Task<InstanceOperationOutcome> SaveBackupAsync(
        string instanceId,
        string destinationPath,
        CancellationToken cancellationToken = default);

    Task<InstanceOperationOutcome> ImportAsync(
        string archivePath,
        CancellationToken cancellationToken = default);
}
