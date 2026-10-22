namespace SlcMinecraftLaunch.Domain.Models;

public sealed record InstanceLaunchSettings(
    int MinMemoryMb = 1024,
    int MaxMemoryMb = 4096,
    int? SuggestedJavaMajor = null,
    string? JavaExecutablePath = null,
    IReadOnlyList<string>? JvmArguments = null,
    IReadOnlyList<string>? GameArguments = null,
    int WindowWidth = 854,
    int WindowHeight = 480,
    bool Fullscreen = false);
