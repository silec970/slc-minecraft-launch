using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Domain.Abstractions;

public interface IDownloadTaskService
{
    event EventHandler<DownloadTaskRecord>? TaskChanged;

    string? ActiveTaskId { get; }

    Task<IReadOnlyList<DownloadTaskRecord>> GetTasksAsync(
        CancellationToken cancellationToken = default);

    Task<DownloadTaskRecord> StartAsync(
        ResourceCompletionRequest request,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<DownloadTaskRecord?> PauseAsync(
        string taskId,
        CancellationToken cancellationToken = default);

    Task<DownloadTaskRecord> ResumeAsync(
        string taskId,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<DownloadTaskRecord> RetryFailedAsync(
        string taskId,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        string taskId,
        CancellationToken cancellationToken = default);
}
