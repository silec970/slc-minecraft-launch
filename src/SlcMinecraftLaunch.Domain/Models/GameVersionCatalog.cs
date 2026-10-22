namespace SlcMinecraftLaunch.Domain.Models;

public sealed record GameVersionEntry(
    GameVersionDescriptor Version,
    bool IsFavorite,
    bool IsIsolated);

public sealed record GameVersionGroup(
    LoaderKind LoaderKind,
    string NameKey,
    IReadOnlyList<GameVersionEntry> Versions);

public sealed record GameVersionCatalog(
    string RootPath,
    IReadOnlyList<GameVersionGroup> Groups)
{
    public IReadOnlyList<GameVersionEntry> AllVersions =>
        Groups.SelectMany(group => group.Versions).ToArray();
}

