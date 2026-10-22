using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Domain.Abstractions;

public interface ISettingsService
{
    LauncherSettingsSnapshot GetSnapshot();

    Task<UiSettings> LoadUiSettingsAsync(CancellationToken cancellationToken = default);

    Task SaveUiSettingsAsync(
        UiSettings settings,
        CancellationToken cancellationToken = default);
}

