using SlcMinecraftLaunch.Application;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Tests;

public sealed class DownloadTaskServiceTests
{
    [Fact]
    public async Task Start_PersistsFailedFilesAndSourceHealth()
    {
        var store = new InMemoryDownloadTaskStore();
        var health = new RecordingSourceHealthService();
        var runtime = new FakeRuntime(async (_, observer, _, _) =>
        {
            observer?.OnFileCompleted(new ResourceDownloadFileResult(
                "library.jar",
                @"C:\Minecraft\libraries\library.jar",
                "https://good.example/",
                "https://good.example/library.jar",
                100,
                "ABC",
                true));
            observer?.OnFileCompleted(new ResourceDownloadFileResult(
                "asset.bin",
                @"C:\Minecraft\assets\asset.bin",
                "https://bad.example/",
                "https://bad.example/asset.bin",
                200,
                "DEF",
                false,
                "timeout"));
            return await Task.FromResult(new ResourceCompletionOutcome(
                false,
                "Download.ResourceCompletionFailed",
                "one file failed",
                1));
        });
        var service = new DownloadTaskService(
            new FakeRuntimeProvider(runtime),
            store,
            health);

        var record = await service.StartAsync(
            new ResourceCompletionRequest("1.21.4", @"C:\Java\21\java.exe"));

        Assert.Equal(DownloadTaskStatus.Failed, record.Status);
        var failed = Assert.Single(record.FailedFiles);
        Assert.Equal("asset.bin", failed.FileName);
        Assert.Equal("timeout", failed.ErrorDetail);
        Assert.Contains("https://bad.example/", record.Sources);
        Assert.Equal(2, health.Observations.Count);
        var persisted = Assert.Single(await store.LoadAsync());
        Assert.Equal(DownloadTaskStatus.Failed, persisted.Status);
    }

    [Fact]
    public async Task PauseCancelsActiveCompleterAndResumeReusesTask()
    {
        var callCount = 0;
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new InMemoryDownloadTaskStore();
        var runtime = new FakeRuntime(async (_, _, _, cancellationToken) =>
        {
            if (Interlocked.Increment(ref callCount) == 1)
            {
                started.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            return new ResourceCompletionOutcome(true, "Download.Completed");
        });
        var service = new DownloadTaskService(
            new FakeRuntimeProvider(runtime),
            store,
            new RecordingSourceHealthService());

        var startTask = service.StartAsync(
            new ResourceCompletionRequest("1.21.4", @"C:\Java\21\java.exe"));
        await started.Task;
        var taskId = Assert.IsType<string>(service.ActiveTaskId);
        var paused = await service.PauseAsync(taskId);
        var firstResult = await startTask;

        Assert.NotNull(paused);
        Assert.Equal(DownloadTaskStatus.Paused, firstResult.Status);
        Assert.Null(service.ActiveTaskId);

        var resumed = await service.ResumeAsync(taskId);

        Assert.Equal(DownloadTaskStatus.Completed, resumed.Status);
        Assert.Equal(2, callCount);
        Assert.Equal(2, resumed.Attempt);
    }

    [Fact]
    public async Task GetTasks_RecoversInterruptedRunningTaskAsPaused()
    {
        var now = DateTimeOffset.UtcNow;
        var store = new InMemoryDownloadTaskStore
        {
            Tasks =
            [
                new DownloadTaskRecord(
                    "interrupted",
                    "1.21.4",
                    @"C:\Java\java.exe",
                    @"C:\Minecraft",
                    DownloadTaskStatus.Running,
                    "Download.Task.Running",
                    0.4,
                    1,
                    now,
                    now,
                    [],
                    [])
            ]
        };
        var service = new DownloadTaskService(
            new FakeRuntimeProvider(new FakeRuntime(
                (_, _, _, _) => Task.FromResult(new ResourceCompletionOutcome(
                    true,
                    "Download.Completed")))),
            store,
            new RecordingSourceHealthService());

        var recovered = Assert.Single(await service.GetTasksAsync());

        Assert.Equal(DownloadTaskStatus.Paused, recovered.Status);
        Assert.Equal("Download.Task.Interrupted", recovered.MessageKey);
        Assert.Equal(DownloadTaskStatus.Paused, Assert.Single(await store.LoadAsync()).Status);
    }

    [Fact]
    public async Task RetryFailed_ReinvokesSameRuntimeAndClearsFailuresOnSuccess()
    {
        var callCount = 0;
        var runtime = new FakeRuntime((_, observer, _, _) =>
        {
            if (Interlocked.Increment(ref callCount) == 1)
            {
                observer?.OnFileCompleted(new ResourceDownloadFileResult(
                    "failed.jar",
                    @"C:\Minecraft\libraries\failed.jar",
                    "https://source.example/",
                    "https://source.example/failed.jar",
                    10,
                    "ABC",
                    false,
                    "temporary"));
                return Task.FromResult(new ResourceCompletionOutcome(
                    false,
                    "Download.ResourceCompletionFailed",
                    FailedFileCount: 1));
            }

            return Task.FromResult(new ResourceCompletionOutcome(
                true,
                "Download.Completed"));
        });
        var service = new DownloadTaskService(
            new FakeRuntimeProvider(runtime),
            new InMemoryDownloadTaskStore(),
            new RecordingSourceHealthService());

        var failed = await service.StartAsync(
            new ResourceCompletionRequest("1.21.4", @"C:\Java\java.exe"));
        var retried = await service.RetryFailedAsync(failed.TaskId);

        Assert.Equal(DownloadTaskStatus.Failed, failed.Status);
        Assert.Single(failed.FailedFiles);
        Assert.Equal(DownloadTaskStatus.Completed, retried.Status);
        Assert.Empty(retried.FailedFiles);
        Assert.Equal(2, callCount);
    }

    private sealed class FakeRuntime(
        Func<ResourceCompletionRequest, IResourceDownloadObserver?, IProgress<OperationProgress>?, CancellationToken, Task<ResourceCompletionOutcome>> complete)
        : IGameRuntime
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
            => complete(request, null, progress, cancellationToken);

        public Task<ResourceCompletionOutcome> CompleteResourcesObservedAsync(
            ResourceCompletionRequest request,
            IResourceDownloadObserver? observer,
            IProgress<OperationProgress>? progress = null,
            CancellationToken cancellationToken = default)
            => complete(request, observer, progress, cancellationToken);

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

    private sealed class FakeRuntimeProvider(IGameRuntime runtime) : IGameRuntimeProvider
    {
        public Task<IGameRuntime> GetAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(runtime);
    }

    private sealed class InMemoryDownloadTaskStore : IDownloadTaskStore
    {
        private readonly SemaphoreSlim _lock = new(1, 1);

        public IReadOnlyList<DownloadTaskRecord> Tasks { get; set; } = [];

        public async Task<IReadOnlyList<DownloadTaskRecord>> LoadAsync(
            CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                return Tasks.ToArray();
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task SaveAsync(
            IReadOnlyList<DownloadTaskRecord> tasks,
            CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                Tasks = tasks.ToArray();
            }
            finally
            {
                _lock.Release();
            }
        }
    }

    private sealed class RecordingSourceHealthService : ISourceHealthService
    {
        public List<DownloadSourceObservation> Observations { get; } = [];

        public Task<IReadOnlyDictionary<string, DownloadSourceScore>> GetScoresAsync(
            IReadOnlyList<string> sourceRoots,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<string, DownloadSourceScore>>(
                sourceRoots.ToDictionary(
                    source => source,
                    source => new DownloadSourceScore(
                        source,
                        50,
                        0,
                        0,
                        0,
                        DateTimeOffset.UtcNow),
                    StringComparer.OrdinalIgnoreCase));

        public Task RecordAsync(
            IReadOnlyList<DownloadSourceObservation> observations,
            CancellationToken cancellationToken = default)
        {
            Observations.AddRange(observations);
            return Task.CompletedTask;
        }
    }
}
