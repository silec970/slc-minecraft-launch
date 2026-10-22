using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Domain.Abstractions;

public interface IPlayerProfileStore
{
    Task<PlayerProfileSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(
        PlayerProfileSettings settings,
        CancellationToken cancellationToken = default);
}

