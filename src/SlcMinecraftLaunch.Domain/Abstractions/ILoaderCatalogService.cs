using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Domain.Abstractions;

public interface ILoaderCatalogService
{
    Task<IReadOnlyList<LoaderVersionOption>> GetVersionsAsync(
        string minecraftVersion,
        LoaderKind loaderKind,
        CancellationToken cancellationToken = default);
}

