using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Domain.Abstractions;

public interface IVersionService
{
    Task<GameVersionCatalog> GetCatalogAsync(CancellationToken cancellationToken = default);

    Task SetFavoriteAsync(
        string versionId,
        bool isFavorite,
        CancellationToken cancellationToken = default);

    Task SetIsolationAsync(
        string versionId,
        bool isIsolated,
        CancellationToken cancellationToken = default);
}

