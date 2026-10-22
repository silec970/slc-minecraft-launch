using SlcMinecraftLaunch.Application;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Tests;

public sealed class GameLaunchServiceTests
{
    [Fact]
    public async Task Launch_UsesConfiguredOfflinePlayerAndSelectedVersion()
    {
        var runtime = new FakeRuntime();
        var service = new GameLaunchService(
            new FakeRuntimeProvider(runtime),
            new FakeJavaService(),
            new FakePlayerProfileService(),
            new FakePreferencesStore(),
            new FakeInstanceService());

        var result = await service.LaunchAsync("1.21.4-fabric-0.16.9");

        Assert.True(result.Succeeded);
        Assert.Equal("离线玩家", runtime.LastLaunch?.PlayerName);
        Assert.Equal("1.21.4-fabric-0.16.9", runtime.LastLaunch?.VersionId);
        Assert.True(runtime.LastLaunch?.VersionIsolation);
        Assert.Equal(2048, runtime.LastLaunch?.InstanceSettings?.MinMemoryMb);
        Assert.Equal(6144, runtime.LastLaunch?.InstanceSettings?.MaxMemoryMb);
    }

    private sealed class FakeRuntime : IGameRuntime
    {
        public event EventHandler<GameExitNotification>? GameExited
        {
            add { }
            remove { }
        }

        public string RootPath => @"C:\Minecraft";
        public GameLaunchRequest? LastLaunch { get; private set; }

        public Task<GameRuntimeSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new GameRuntimeSnapshot(RootPath, ["1.21.4-fabric-0.16.9"], true));

        public Task<IReadOnlyList<GameVersionDescriptor>> GetVersionsAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<GameVersionDescriptor>>(
            [
                new(
                    "1.21.4-fabric-0.16.9",
                    "1.21.4-fabric-0.16.9",
                    "1.21.4",
                    LoaderKind.Fabric,
                    "0.16.9",
                    21,
                    "java-runtime-delta",
                    false)
            ]);

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

        public Task<GameLaunchOutcome> LaunchOfflineAsync(
            GameLaunchRequest request,
            IProgress<OperationProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            LastLaunch = request;
            return Task.FromResult(new GameLaunchOutcome(true, "Launch.Started"));
        }

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

    private sealed class FakePlayerProfileService : IPlayerProfileService
    {
        public Task<string> GetPlayerNameAsync(CancellationToken cancellationToken = default)
            => Task.FromResult("离线玩家");

        public Task<PlayerProfileSaveResult> SavePlayerNameAsync(
            string playerName,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new PlayerProfileSaveResult(
                true,
                "Settings.PlayerNameSaved",
                playerName));
    }

    private sealed class FakePreferencesStore : IVersionPreferencesStore
    {
        public Task<IReadOnlyDictionary<string, VersionPreference>> LoadAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<string, VersionPreference>>(
                new Dictionary<string, VersionPreference>
                {
                    ["1.21.4-fabric-0.16.9"] = new(IsIsolated: true)
                });

        public Task SaveAsync(
            IReadOnlyDictionary<string, VersionPreference> preferences,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeInstanceService : IInstanceService
    {
        private readonly InstanceManifest _manifest = new(
            "1.21.4-fabric-0.16.9",
            "Fabric Test",
            "1.21.4",
            LoaderKind.Fabric,
            "0.16.9",
            new InstanceLaunchSettings(2048, 6144),
            false,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        public Task<IReadOnlyList<GameInstance>> GetInstancesAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<GameInstance>>([]);

        public Task<InstanceManifest?> GetManifestAsync(
            string instanceId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<InstanceManifest?>(_manifest);

        public Task<InstanceOperationOutcome> CreateEmptyAsync(
            NewInstanceRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<InstanceOperationOutcome> SaveAsync(
            InstanceManifest manifest,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<InstanceOperationOutcome> DeleteAsync(
            string instanceId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<InstanceOperationOutcome> CopyAsync(
            string sourceInstanceId,
            string targetInstanceId,
            string targetName,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}

