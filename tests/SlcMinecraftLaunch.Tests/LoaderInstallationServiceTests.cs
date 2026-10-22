using SlcMinecraftLaunch.Application;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Tests;

public sealed class LoaderInstallationServiceTests
{
    [Fact]
    public async Task Install_UsesFirstStableLoaderAndVerifiesLocator()
    {
        var expectedId = "1.21.4-fabric-0.16.9";
        var backend = new FakeBackend(expectedId);
        var runtime = new FakeRuntime(expectedId);
        var service = new LoaderInstallationService(
            new FakeCatalog(
                [
                    new LoaderVersionOption("0.17.0-beta", false),
                    new LoaderVersionOption("0.16.9", true)
                ]),
            new FakeJavaService(),
            backend,
            new FakeRuntimeProvider(runtime));

        var result = await service.InstallAsync(
            new LoaderInstallRequest("1.21.4", LoaderKind.Fabric));

        Assert.True(result.Succeeded);
        Assert.Equal(expectedId, result.InstalledVersionId);
        Assert.Equal("0.16.9", backend.Plan?.LoaderVersion);
        Assert.Equal(expectedId, backend.Plan?.CustomVersionId);
    }

    [Fact]
    public async Task Install_ReturnsBackendFailure()
    {
        var backend = new FakeBackend(null);
        var service = new LoaderInstallationService(
            new FakeCatalog([new LoaderVersionOption("0.16.9", true)]),
            new FakeJavaService(),
            backend,
            new FakeRuntimeProvider(new FakeRuntime()));

        var result = await service.InstallAsync(
            new LoaderInstallRequest("1.21.4", LoaderKind.Fabric, "0.16.9"));

        Assert.False(result.Succeeded);
        Assert.Equal("Loader.InstallFailed", result.MessageKey);
    }

    private sealed class FakeCatalog(IReadOnlyList<LoaderVersionOption> versions) : ILoaderCatalogService
    {
        public Task<IReadOnlyList<LoaderVersionOption>> GetVersionsAsync(
            string minecraftVersion,
            LoaderKind loaderKind,
            CancellationToken cancellationToken = default)
            => Task.FromResult(versions);
    }

    private sealed class FakeJavaService : IJavaRuntimeService
    {
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

    private sealed class FakeBackend(string? installedVersionId) : ILoaderInstallBackend
    {
        public LoaderInstallPlan? Plan { get; private set; }

        public Task<LoaderInstallOutcome> InstallAsync(
            LoaderInstallPlan plan,
            IProgress<OperationProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            Plan = plan;
            return Task.FromResult(installedVersionId is null
                ? new LoaderInstallOutcome(false, "Loader.InstallFailed")
                : new LoaderInstallOutcome(true, "Loader.Installed", installedVersionId));
        }
    }

    private sealed class FakeRuntime(params string[] versions) : IGameRuntime
    {
        public event EventHandler<GameExitNotification>? GameExited
        {
            add { }
            remove { }
        }

        public string RootPath => @"C:\Minecraft";

        public Task<GameRuntimeSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new GameRuntimeSnapshot(RootPath, versions, true));

        public Task<IReadOnlyList<GameVersionDescriptor>> GetVersionsAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<GameVersionDescriptor>>(
                versions.Select(version => new GameVersionDescriptor(
                    version,
                    version,
                    "1.21.4",
                    LoaderKind.Fabric,
                    "0.16.9",
                    21,
                    "java-runtime-delta",
                    false)).ToArray());

        public Task<ResourceCompletionOutcome> CompleteResourcesAsync(
            ResourceCompletionRequest request,
            IProgress<OperationProgress>? progress = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ResourceCompletionOutcome(true, "Download.Completed"));

        public Task<ResourceCompletionOutcome> DownloadModAsync(
            ModDownloadRequest request,
            IProgress<OperationProgress>? progress = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ResourceCompletionOutcome(true, "Mods.Downloaded"));

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
}
