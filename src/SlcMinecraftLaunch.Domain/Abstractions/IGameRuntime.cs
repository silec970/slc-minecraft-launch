using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Domain.Abstractions;

public interface IGameRuntime : IAsyncDisposable
{
    Task<GameRuntimeSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
}

