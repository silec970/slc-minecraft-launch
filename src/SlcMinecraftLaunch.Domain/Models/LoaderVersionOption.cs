namespace SlcMinecraftLaunch.Domain.Models;

public sealed record LoaderVersionOption(
    string Version,
    bool IsStable,
    string? DisplayName = null);

