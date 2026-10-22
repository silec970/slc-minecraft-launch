using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Application;

public sealed class DownloadService(IDownloadTaskService downloadTaskService) : IDownloadService
{
    public async Task<ResourceCompletionOutcome> CompleteResourcesAsync(
        ResourceCompletionRequest request,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        DownloadTaskRecord task;
        try
        {
            task = await downloadTaskService
                .StartAsync(request, progress, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException exception) when (
            exception.Message == "Download.Task.AlreadyRunning")
        {
            return new ResourceCompletionOutcome(false, "Download.Task.AlreadyRunning");
        }

        return new ResourceCompletionOutcome(
            task.Status == DownloadTaskStatus.Completed,
            task.MessageKey,
            task.ErrorDetail,
            task.FailedFiles.Count);
    }
}
