using SlcMinecraftLaunch.Application;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Tests;

public sealed class VanillaInstallationServiceTests
{
    [Fact]
    public async Task Install_ComposesMaterializationJavaAndResourceCompletion()
    {
        var materializer = new FakeMaterializer();
        var runtime = new FakeRuntime();
        var download = new FakeDownloadService();
        var service = new VanillaInstallationService(
            materializer,
            new FakeRuntimeProvider(runtime),
            new FakeJavaService(),
            download);

        var result = await service.InstallAsync("1.21.4");

        Assert.True(result.Succeeded);
        Assert.Equal("1.21.4", materializer.VersionId);
        Assert.True(download.Completed);
    }

    private sealed class FakeMaterializer : IGameVersionMaterializer
    {
        public string? VersionId { get; private set; }

        public Task EnsureVersionAsync(
            string rootPath,
            string targetVersion,
            IProgress<OperationProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            VersionId = targetVersion;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRuntime : IGameRuntime
    {
        public event EventHandler<GameExitNotification>? GameExited
        {
            add { }
            remove { }
        }

        public string RootPath => @"C:\Minecraft";
        public bool ResourcesCompleted { get; private set; }

        public Task<GameRuntimeSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new GameRuntimeSnapshot(RootPath, ["1.21.4"], true));

        public Task<IReadOnlyList<GameVersionDescriptor>> GetVersionsAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<GameVersionDescriptor>>(
            [
                new("1.21.4", "1.21.4", "1.21.4", LoaderKind.Vanilla, null, 21,
                    "java-runtime-delta", false)
            ]);

        public Task<ResourceCompletionOutcome> CompleteResourcesAsync(
            ResourceCompletionRequest request,
            IProgress<OperationProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            ResourcesCompleted = true;
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

    private sealed class FakeRuntimeProvider(IGameRuntime runtime) : IGameRuntimeProvider
    {
        public Task<IGameRuntime> GetAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(runtime);
    }

    private sealed class FakeJavaService : IJavaRuntimeService
    {
        public Task<JavaRuntimeProvisionOutcome> ProvisionMajorAsync(
            int majorVersion,
            IProgress<OperationProgress>? progress = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new JavaRuntimeProvisionOutcome(
                true,
                @"C:\Java\21\java.exe",
                "Java.Ready"));

        public Task<JavaRuntimeResolution> EnsureAsync(
            string versionId,
            LoaderKind loaderKind,
            string? explicitJavaPath = null,
            IProgress<OperationProgress>? progress = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new JavaRuntimeResolution(
                new JavaCandidate(@"C:\Java\21\java.exe", 21),
                Array.Empty<JavaSelectionWarning>(),
                false,
                "Java.Ready"));
    }

    private sealed class FakeDownloadService : IDownloadService
    {
        public bool Completed { get; private set; }

        public Task<ResourceCompletionOutcome> CompleteResourcesAsync(
            ResourceCompletionRequest request,
            IProgress<OperationProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            Completed = true;
            return Task.FromResult(new ResourceCompletionOutcome(true, "Download.Completed"));
        }
    }
}
