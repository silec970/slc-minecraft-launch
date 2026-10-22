namespace SlcMinecraftLaunch.Domain.Models;

public sealed record InstanceExportOptions(
    bool IncludeSaves = false,
    bool IncludeScreenshots = false,
    bool IncludeServersFile = false,
    bool IncludeLogs = false,
    bool IncludeDefaultGameplayData = true);

public sealed record InstanceLoaderDeclaration(
    string InstanceId,
    string MinecraftVersion,
    LoaderKind LoaderKind,
    string? LoaderVersion);

public sealed record ImportedInstanceArchive(
    string ArchivePath,
    InstanceManifest Manifest,
    InstanceLoaderDeclaration LoaderDeclaration,
    IReadOnlyDictionary<string, ModSourceMetadata> ModSources);
