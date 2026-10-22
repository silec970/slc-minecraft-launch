using SlcMinecraftLaunch.Application;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Tests;

public sealed class JavaRuntimeServiceTests
{
    [Fact]
    public async Task Ensure_ProvisionsExactJavaWhenForgeCannotUseNewerRuntime()
    {
        var runtime = CreateRuntime("1.21.4-forge-54.0.0", "1.21.4", LoaderKind.Forge);
        var discovery = new FakeJavaDiscovery(
            [new JavaCandidate(@"C:\Java\25\java.exe", 25)],
            [new JavaCandidate(@"C:\Java\21\java.exe", 21)]);
        var provisioner = new FakeJavaProvisioner();
        var service = new JavaRuntimeService(
            discovery,
            provisioner,
            new FakeRuntimeProvider(runtime),
            new JavaSelectionPolicy());

        var result = await service.EnsureAsync(
            runtime.Versions[0].Id,
            LoaderKind.Forge);

        Assert.Equal(21, result.Selected?.MajorVersion);
        Assert.True(result.WasProvisioned);
        Assert.Equal(21, provisioner.RequestedMajor);
        Assert.Equal("java-runtime-delta", provisioner.RequestedComponent);
    }

    [Fact]
    public async Task Ensure_UsesCompatibleExistingJavaWithoutDownload()
    {
        var runtime = CreateRuntime("1.21.4-fabric-0.16.9", "1.21.4", LoaderKind.Fabric);
        var discovery = new FakeJavaDiscovery(
            [new JavaCandidate(@"C:\Java\21\java.exe", 21)],
            []);
        var provisioner = new FakeJavaProvisioner();
        var service = new JavaRuntimeService(
            discovery,
            provisioner,
            new FakeRuntimeProvider(runtime),
            new JavaSelectionPolicy());

        var result = await service.EnsureAsync(
            runtime.Versions[0].Id,
            LoaderKind.Fabric);

        Assert.Equal(21, result.Selected?.MajorVersion);
        Assert.False(result.WasProvisioned);
        Assert.Null(provisioner.RequestedMajor);
    }

    private static FakeRuntime CreateRuntime(
        string id,
        string baseVersion,
        LoaderKind loaderKind)
    {
        return new FakeRuntime(new GameVersionDescriptor(
            id,
            id,
            baseVersion,
            loaderKind,
            "1.0",
            21,
            "java-runtime-delta",
            false));
    }

    private sealed class FakeRuntime(params GameVersionDescriptor[] versions) : IGameRuntime
    {
        public event EventHandler<GameExitNotification>? GameExited
        {
            add { }
            remove { }
        }

        public IReadOnlyList<GameVersionDescriptor> Versions { get; } = versions;
        public string RootPath => @"C:\Minecraft";

        public Task<GameRuntimeSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new GameRuntimeSnapshot(RootPath, Versions.Select(v => v.Id).ToArray(), true));

        public Task<IReadOnlyList<GameVersionDescriptor>> GetVersionsAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<GameVersionDescriptor>>(Versions);

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

    private sealed class FakeJavaDiscovery(
        IReadOnlyList<JavaCandidate> discovered,
        IReadOnlyList<JavaCandidate> inspectable) : IJavaDiscovery
    {
        public Task<IReadOnlyList<JavaCandidate>> DiscoverAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(discovered);

        public Task<JavaCandidate?> InspectAsync(
            string executablePath,
            CancellationToken cancellationToken = default)
            => Task.FromResult(inspectable.FirstOrDefault(candidate =>
                string.Equals(candidate.ExecutablePath, executablePath, StringComparison.OrdinalIgnoreCase)));
    }

    private sealed class FakeJavaProvisioner : IJavaRuntimeProvisioner
    {
        public int? RequestedMajor { get; private set; }
        public string? RequestedComponent { get; private set; }

        public Task<JavaRuntimeProvisionOutcome> EnsureAsync(
            int requiredMajorVersion,
            string? mojangComponent,
            IProgress<OperationProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            RequestedMajor = requiredMajorVersion;
            RequestedComponent = mojangComponent;
            return Task.FromResult(new JavaRuntimeProvisionOutcome(
                true,
                $@"C:\Java\{requiredMajorVersion}\java.exe",
                "Java.MojangRuntimeReady"));
        }
    }
}

