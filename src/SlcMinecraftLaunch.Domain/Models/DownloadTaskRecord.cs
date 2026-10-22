namespace SlcMinecraftLaunch.Domain.Models;

public enum DownloadTaskStatus
{
    Queued,
    Running,
    Paused,
    Failed,
    Completed
}

public sealed record DownloadTaskFileRecord(
    string FileName,
    string TargetPath,
    string? SourceUrl,
    long FileSize = 0,
    string? CheckSum = null,
    string? ErrorDetail = null);

public sealed record DownloadTaskRecord(
    string TaskId,
    string VersionId,
    string JavaPath,
    string GameRoot,
    DownloadTaskStatus Status,
    string MessageKey,
    double Progress,
    int Attempt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<DownloadTaskFileRecord> FailedFiles,
    IReadOnlyList<string> Sources,
    string? ErrorDetail = null)
{
    public bool CanPause => Status == DownloadTaskStatus.Running;
    public bool CanRetry => Status is DownloadTaskStatus.Paused or DownloadTaskStatus.Failed;
    public bool CanDelete => Status is not DownloadTaskStatus.Running;
}
