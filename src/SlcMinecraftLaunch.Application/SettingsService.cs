using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;
using SlcMinecraftLaunch.Infrastructure.Configuration;

namespace SlcMinecraftLaunch.Application;

public sealed class SettingsService(
    PortableLauncherPaths paths,
    IUiSettingsStore uiSettingsStore) : ISettingsService
{
    public LauncherSettingsSnapshot GetSnapshot()
    {
        return new LauncherSettingsSnapshot(
            paths.GameRoot,
            paths.DataRoot,
            "0.1.0",
            Path.Combine(paths.DataRoot, "logs"));
    }

    public Task<UiSettings> LoadUiSettingsAsync(CancellationToken cancellationToken = default)
    {
        return uiSettingsStore.LoadAsync(cancellationToken);
    }

    public Task SaveUiSettingsAsync(
        UiSettings settings,
        CancellationToken cancellationToken = default)
    {
        return uiSettingsStore.SaveAsync(settings, cancellationToken);
    }
}

