using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Domain.Abstractions;

public interface IUiSettingsStore
{
    Task<UiSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(UiSettings settings, CancellationToken cancellationToken = default);
}

