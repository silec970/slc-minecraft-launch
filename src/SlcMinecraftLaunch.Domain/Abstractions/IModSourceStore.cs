using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Domain.Abstractions;

public interface IModSourceStore
{
    Task<IReadOnlyDictionary<string, ModSourceMetadata>> LoadAsync(
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        IReadOnlyDictionary<string, ModSourceMetadata> sources,
        CancellationToken cancellationToken = default);
}
