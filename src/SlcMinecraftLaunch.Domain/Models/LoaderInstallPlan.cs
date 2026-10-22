namespace SlcMinecraftLaunch.Domain.Models;

public sealed record LoaderInstallPlan(
    string MinecraftVersion,
    LoaderKind LoaderKind,
    string LoaderVersion,
    string JavaPath,
    string CustomVersionId);

