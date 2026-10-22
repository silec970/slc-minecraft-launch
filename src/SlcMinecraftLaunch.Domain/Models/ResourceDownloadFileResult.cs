namespace SlcMinecraftLaunch.Domain.Models;

public sealed record ResourceDownloadFileResult(
    string FileName,
    string TargetPath,
    string? SourceRoot,
    string? SourceUrl,
    long FileSize,
    string? CheckSum,
    bool Succeeded,
    string? ErrorDetail = null);
