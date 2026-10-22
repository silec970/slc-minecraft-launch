using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Domain.Abstractions;

public interface IPlayerProfileService
{
    Task<string> GetPlayerNameAsync(CancellationToken cancellationToken = default);

    Task<PlayerProfileSaveResult> SavePlayerNameAsync(
        string playerName,
        CancellationToken cancellationToken = default);
}

