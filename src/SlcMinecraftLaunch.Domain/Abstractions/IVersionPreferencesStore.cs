using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Domain.Abstractions;

public interface IVersionPreferencesStore
{
    Task<IReadOnlyDictionary<string, VersionPreference>> LoadAsync(
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        IReadOnlyDictionary<string, VersionPreference> preferences,
        CancellationToken cancellationToken = default);
}

