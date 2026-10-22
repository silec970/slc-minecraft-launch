namespace SlcMinecraftLaunch.Domain.Models;

public sealed record DownloadTaskFileActivity(
    string TaskId,
    DateTimeOffset CompletedAt,
    ResourceDownloadFileResult File);
