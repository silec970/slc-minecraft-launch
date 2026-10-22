namespace SlcMinecraftLaunch.Domain.Models;

public sealed record ModDownloadCandidate(
    ModProvider Provider,
    string ProjectId,
    string? VersionId,
    string FileName,
    string DownloadUrl,
    string? Sha1,
    long Size,
    IReadOnlyList<ModDependencySource>? Dependencies = null);

public sealed record ModDependencySource(
    string ModId,
    ModProvider Provider,
    string? ProjectId,
    string? VersionId,
    bool IsRequired,
    string? VersionRange = null);

public sealed record ModSourceMetadata(
    string ModId,
    ModProvider Provider,
    string ProjectId,
    string? VersionId,
    IReadOnlyList<ModDependencySource> Dependencies);

