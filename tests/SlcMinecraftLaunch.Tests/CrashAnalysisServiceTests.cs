using SlcMinecraftLaunch.Application;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Tests;

public sealed class CrashAnalysisServiceTests
{
    [Fact]
    public async Task Analyze_UsesDefaultIsolationAndPersistsReport()
    {
        var backend = new FakeBackend();
        var store = new FakeStore();
        var service = new CrashAnalysisService(
            new FakeRuntimeProvider(new FakeRuntime()),
            new FakePreferencesStore(),
            backend,
            store);

        var report = await service.AnalyzeAsync("1.21.4", 1);

        Assert.True(backend.Request?.VersionIsolation);
        Assert.Equal(1, backend.Request?.ExitCode);
        Assert.Single(report.Insights);
        Assert.Same(report, store.Report);
    }

    [Fact]
    public async Task Coordinator_AnalyzesGameExitInBackground()
    {
        var runtime = new FakeRuntime();
        var service = new RecordingCrashService();
        await using var coordinator = new CrashAnalysisCoordinator(service);
        coordinator.Start(runtime);

        runtime.RaiseExit(new GameExitNotification("1.21.4", 2, DateTimeOffset.Now));
        var report = await service.Analyzed.Task.WaitAsync(TimeSpan.FromSeconds(3));

        Assert.Equal("1.21.4", report.VersionId);
    }

    private sealed class FakeRuntime : IGameRuntime
    {
        public event EventHandler<GameExitNotification>? GameExited;
        public string RootPath => @"C:\Minecraft";

        public void RaiseExit(GameExitNotification notification)
            => GameExited?.Invoke(this, notification);

        public Task<GameRuntimeSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new GameRuntimeSnapshot(RootPath, ["1.21.4"], true));

        public Task<IReadOnlyList<GameVersionDescriptor>> GetVersionsAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<GameVersionDescriptor>>([]);

        public Task<ResourceCompletionOutcome> CompleteResourcesAsync(
            ResourceCompletionRequest request,
            IProgress<OperationProgress>? progress = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ResourceCompletionOutcome(true, "Download.Completed"));

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

    private sealed class FakePreferencesStore : IVersionPreferencesStore
    {
        public Task<IReadOnlyDictionary<string, VersionPreference>> LoadAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<string, VersionPreference>>(
                new Dictionary<string, VersionPreference>());

        public Task SaveAsync(
            IReadOnlyDictionary<string, VersionPreference> preferences,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeBackend : ICrashAnalysisBackend
    {
        public CrashAnalysisRequest? Request { get; private set; }

        public Task<IReadOnlyList<CrashInsight>> AnalyzeAsync(
            CrashAnalysisRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult<IReadOnlyList<CrashInsight>>(
            [
                new CrashInsight(
                    "NoEnoughMemory",
                    CrashCategory.Memory,
                    CrashSeverity.Critical,
                    "Crash.Category.Memory.Title",
                    "Crash.Category.Memory.Suggestion")
            ]);
        }
    }

    private sealed class FakeStore : ICrashReportStore
    {
        public CrashAnalysisReport? Report { get; private set; }

        public Task<string> SaveAsync(
            CrashAnalysisReport report,
            CancellationToken cancellationToken = default)
        {
            Report = report;
            return Task.FromResult(@"C:\report.json");
        }
    }

    private sealed class RecordingCrashService : ICrashAnalysisService
    {
        public TaskCompletionSource<CrashAnalysisReport> Analyzed { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<CrashAnalysisReport> AnalyzeAsync(
            string versionId,
            int? exitCode = null,
            CancellationToken cancellationToken = default)
        {
            var report = new CrashAnalysisReport(
                versionId,
                DateTimeOffset.Now,
                exitCode,
                Array.Empty<CrashInsight>());
            Analyzed.TrySetResult(report);
            return Task.FromResult(report);
        }
    }
}
