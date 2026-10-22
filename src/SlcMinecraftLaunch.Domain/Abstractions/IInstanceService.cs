using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Domain.Abstractions;

public interface IInstanceService
{
    Task<IReadOnlyList<GameInstance>> GetInstancesAsync(
        CancellationToken cancellationToken = default);

    Task<InstanceManifest?> GetManifestAsync(
        string instanceId,
        CancellationToken cancellationToken = default);

    Task<InstanceOperationOutcome> CreateEmptyAsync(
        NewInstanceRequest request,
        CancellationToken cancellationToken = default);

    Task<InstanceOperationOutcome> SaveAsync(
        InstanceManifest manifest,
        CancellationToken cancellationToken = default);

    Task<InstanceOperationOutcome> DeleteAsync(
        string instanceId,
        CancellationToken cancellationToken = default);

    Task<InstanceOperationOutcome> CopyAsync(
        string sourceInstanceId,
        string targetInstanceId,
        string targetName,
        CancellationToken cancellationToken = default);
}
