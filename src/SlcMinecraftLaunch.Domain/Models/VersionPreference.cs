namespace SlcMinecraftLaunch.Domain.Models;

public sealed record VersionPreference(
    bool IsFavorite = false,
    bool IsIsolated = true);

