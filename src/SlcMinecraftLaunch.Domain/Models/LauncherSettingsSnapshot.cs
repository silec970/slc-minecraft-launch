namespace SlcMinecraftLaunch.Domain.Models;

public sealed record LauncherSettingsSnapshot(
    string GameRoot,
    string DataRoot,
    string Version,
    string? LogDirectory = null);

