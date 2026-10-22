namespace SlcMinecraftLaunch.Domain.Models;

public sealed record DownloadSourceOptions(
    IReadOnlyList<string> VersionMetadataRoots,
    IReadOnlyList<string> AssetIndexRoots,
    IReadOnlyList<string> AssetObjectRoots,
    IReadOnlyList<string> LibraryRoots,
    IReadOnlyList<string> ForgeFileRoots,
    IReadOnlyList<string> ForgeMavenRoots,
    IReadOnlyList<string> FabricMavenRoots,
    IReadOnlyList<string> QuiltMavenRoots);
