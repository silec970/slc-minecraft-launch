using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;
using SlcMinecraftLaunch.Infrastructure.Configuration;

namespace SlcMinecraftLaunch.Application;

public sealed class SettingsService(PortableLauncherPaths paths) : ISettingsService
{
    public LauncherSettingsSnapshot GetSnapshot()
    {
        return new LauncherSettingsSnapshot(paths.GameRoot, paths.DataRoot, "0.1.0");
    }
}

