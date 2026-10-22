using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Application;

public sealed class LaunchApplicationService(IGameRuntimeProvider runtimeProvider)
{
    public async Task<GameRuntimeSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var runtime = await runtimeProvider.GetAsync(cancellationToken).ConfigureAwait(false);
        return await runtime.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
    }
}

