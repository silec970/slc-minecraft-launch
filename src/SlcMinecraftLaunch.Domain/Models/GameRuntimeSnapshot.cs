namespace SlcMinecraftLaunch.Domain.Models;

public sealed record GameRuntimeSnapshot(
    string RootPath,
    IReadOnlyList<string> VersionIds,
    bool IsReady);

