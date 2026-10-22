namespace SlcMinecraftLaunch.Domain.Models;

public sealed record ModInstallRequest(
    ModProvider Provider,
    string ProjectId,
    string MinecraftVersion,
    LoaderKind LoaderKind);

public sealed record LocalModInstallRequest(
    string SourceFilePath,
    string MinecraftVersion,
    LoaderKind LoaderKind);

