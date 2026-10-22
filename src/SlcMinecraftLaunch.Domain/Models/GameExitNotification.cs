namespace SlcMinecraftLaunch.Domain.Models;

public sealed record GameExitNotification(
    string VersionId,
    int ExitCode,
    DateTimeOffset ExitedAt,
    string? ExceptionDetail = null);

