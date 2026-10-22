namespace SlcMinecraftLaunch.Domain.Models;

public sealed record LoaderInstallRequest(
    string MinecraftVersion,
    LoaderKind LoaderKind,
    string? LoaderVersion = null,
    string? JavaPath = null,
    string? CustomVersionId = null);

