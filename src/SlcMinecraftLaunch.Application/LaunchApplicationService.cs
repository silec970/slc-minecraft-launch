using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Application;

public sealed class LaunchApplicationService(IGameRuntime gameRuntime)
{
    public Task<GameRuntimeSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        return gameRuntime.GetSnapshotAsync(cancellationToken);
    }
}

