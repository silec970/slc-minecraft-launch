using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Domain.Abstractions;

public interface IGameRuntime : IAsyncDisposable
{
    Task<GameRuntimeSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);

    Task<SmokeLaunchOutcome> LaunchSmokeAsync(
        SmokeLaunchRequest request,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

