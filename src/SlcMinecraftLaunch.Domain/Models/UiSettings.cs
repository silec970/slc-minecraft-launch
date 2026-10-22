namespace SlcMinecraftLaunch.Domain.Models;

public sealed record UiSettings(
    UiTheme Theme = UiTheme.Light,
    string Language = "zh-CN",
    bool AnimationsEnabled = true,
    bool BackgroundEnabled = true,
    bool MusicEnabled = false,
    double MusicVolume = 0.45);

