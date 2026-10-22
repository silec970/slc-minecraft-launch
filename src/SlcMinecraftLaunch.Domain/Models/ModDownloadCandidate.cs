namespace SlcMinecraftLaunch.Domain.Models;

public sealed record ModDownloadCandidate(
    ModProvider Provider,
    string ProjectId,
    string FileName,
    string DownloadUrl,
    string? Sha1,
    long Size);

