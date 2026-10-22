namespace SlcMinecraftLaunch.Domain.Models;

public sealed record ModDownloadRequest(
    string VersionId,
    string ModsDirectory,
    string Url,
    string FileName,
    string? Sha1,
    long Size);

