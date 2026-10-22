using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Domain.Abstractions;

public interface IModProviderCatalog
{
    Task<ModSearchOutcome> SearchAsync(
        ModSearchQuery query,
        CancellationToken cancellationToken = default);

    Task<ModDownloadCandidate?> ResolveDownloadAsync(
        ModInstallRequest request,
        CancellationToken cancellationToken = default);
}

