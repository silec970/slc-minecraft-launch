using SlcMinecraftLaunch.Application;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Tests;

public sealed class DownloadServiceTests
{
    [Fact]
    public async Task CompleteResources_ForwardsProgressAndCancellation()
    {
        var runtime = new FakeRuntime();
        var service = new DownloadService(new FakeDownloadTaskService(runtime));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var reports = new List<OperationProgress>();
        var progress = new InlineProgress<OperationProgress>(reports.Add);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.CompleteResourcesAsync(
                new ResourceCompletionRequest("1.21.4", @"C:\Java\java.exe"),
                progress,
                cancellation.Token));

        var reported = Assert.Single(reports);
        Assert.Equal("Progress.CompletingResources", reported.MessageKey);
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    private sealed class FakeRuntime : IGameRuntime
    {
        public event EventHandler<GameExitNotification>? GameExited
        {
            add { }
            remove { }
        }

        public string RootPath => @"C:\Minecraft";

        public Task<GameRuntimeSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new GameRuntimeSnapshot(RootPath, ["1.21.4"], true));

        public Task<IReadOnlyList<GameVersionDescriptor>> GetVersionsAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<GameVersionDescriptor>>([]);

        public Task<ResourceCompletionOutcome> CompleteResourcesAsync(
            ResourceCompletionRequest request,
            IProgress<OperationProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            progress?.Report(new OperationProgress(
                SmokeTestStage.CompletingResources,
                "Progress.CompletingResources",
                0));
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new ResourceCompletionOutcome(true, "Download.Completed"));
        }

        public Task<ResourceCompletionOutcome> DownloadModAsync(
            ModDownloadRequest request,
            IProgress<OperationProgress>? progress = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ResourceCompletionOutcome(true, "Mods.Downloaded"));

        public Task<GameLaunchOutcome> LaunchOfflineAsync(
            GameLaunchRequest request,
            IProgress<OperationProgress>? progress = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new GameLaunchOutcome(true, "Launch.Started"));

        public Task<SmokeLaunchOutcome> LaunchSmokeAsync(
            SmokeLaunchRequest request,
            IProgress<OperationProgress>? progress = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeDownloadTaskService(IGameRuntime runtime) : IDownloadTaskService
    {
        public event EventHandler<DownloadTaskRecord>? TaskChanged
        {
            add { }
            remove { }
        }

        public event EventHandler<DownloadTaskFileActivity>? FileCompleted
        {
            add { }
            remove { }
        }

        public string? ActiveTaskId => null;

        public Task<IReadOnlyList<DownloadTaskRecord>> GetTasksAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DownloadTaskRecord>>([]);

        public async Task<DownloadTaskRecord> StartAsync(
            ResourceCompletionRequest request,
            IProgress<OperationProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var result = await runtime.CompleteResourcesAsync(
                request,
                progress,
                cancellationToken);
            var now = DateTimeOffset.UtcNow;
            return new DownloadTaskRecord(
                Guid.NewGuid().ToString("N"),
                request.VersionId,
                request.JavaPath,
                runtime.RootPath,
                result.Succeeded ? DownloadTaskStatus.Completed : DownloadTaskStatus.Failed,
                result.MessageKey,
                result.Succeeded ? 1 : 0,
                1,
                now,
                now,
                [],
                []);
        }

        public Task<DownloadTaskRecord?> PauseAsync(
            string taskId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DownloadTaskRecord> ResumeAsync(
            string taskId,
            IProgress<OperationProgress>? progress = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DownloadTaskRecord> RetryFailedAsync(
            string taskId,
            IProgress<OperationProgress>? progress = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> DeleteAsync(
            string taskId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
