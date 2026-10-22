using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Domain.Abstractions;

public interface ISettingsService
{
    LauncherSettingsSnapshot GetSnapshot();
}

