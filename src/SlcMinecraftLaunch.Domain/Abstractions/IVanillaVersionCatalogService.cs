using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Domain.Abstractions;

public interface IVanillaVersionCatalogService
{
    Task<IReadOnlyList<MinecraftReleaseDescriptor>> GetVersionsAsync(
        CancellationToken cancellationToken = default);
}

