namespace SlcMinecraftLaunch.Domain.Models;

public sealed record ModRequirement(
    string ModId,
    bool IsMandatory,
    string? VersionRange = null,
    IReadOnlyList<string>? VersionRanges = null);

public sealed record ModMetadata(
    string ModId,
    string Name,
    string Version,
    IReadOnlyList<ModRequirement> Requirements,
    LoaderKind LoaderKind = LoaderKind.Other);

