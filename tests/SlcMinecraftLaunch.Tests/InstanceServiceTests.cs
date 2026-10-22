using SlcMinecraftLaunch.Application;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;
using SlcMinecraftLaunch.Infrastructure.Configuration;
using SlcMinecraftLaunch.Infrastructure.Instances;

namespace SlcMinecraftLaunch.Tests;

public sealed class InstanceServiceTests
{
    [Fact]
    public async Task CreateEmptyAndSave_PersistInstanceSettings()
    {
        var root = CreateRoot();
        try
        {
            var storage = CreateStorage(root);
            var preferences = new InMemoryPreferencesStore();
            var service = new InstanceService(
                new FakeRuntimeProvider(new FakeRuntime()),
                preferences,
                storage);

            var created = await service.CreateEmptyAsync(
                new NewInstanceRequest("empty", "Empty", "1.21.4", LoaderKind.Vanilla));
            Assert.True(created.Succeeded);
            var manifest = Assert.IsType<InstanceManifest>(await service.GetManifestAsync("empty"));
            Assert.True(manifest.IsPlaceholder);

            var saved = await service.SaveAsync(manifest with
            {
                LaunchSettings = new InstanceLaunchSettings(
                    2048,
                    8192,
                    21,
                    @"C:\Java\21\java.exe",
                    ["-XX:+UseG1GC"],
                    ["--demo"],
                    1280,
                    720,
                    true)
            });

            Assert.True(saved.Succeeded);
            var updated = Assert.IsType<InstanceManifest>(await service.GetManifestAsync("empty"));
            Assert.Equal(8192, updated.LaunchSettings.MaxMemoryMb);
            Assert.Equal(["--demo"], updated.LaunchSettings.GameArguments);
            Assert.True((await preferences.LoadAsync())["empty"].IsIsolated);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task CopyAndDelete_TargetsIndependentLocatorVersionDirectory()
    {
        var root = CreateRoot();
        try
        {
            var storage = CreateStorage(root);
            var preferences = new InMemoryPreferencesStore();
            var manifest = LocalInstanceStorageTests.CreateManifest("source", false);
            await storage.CreatePlaceholderAsync(manifest);
            await LocalInstanceStorageTests.WriteFileAsync(
                Path.Combine(root, "game", "versions", "source", "source.json"),
                """{"id":"source","type":"release","inheritsFrom":"1.21.4","libraries":[],"downloads":{}}""");
            var runtime = new FakeRuntime(new GameVersionDescriptor(
                "source",
                "Source",
                "1.21.4",
                LoaderKind.Forge,
                "54.1.5",
                21,
                "java-runtime-delta",
                false));
            var service = new InstanceService(
                new FakeRuntimeProvider(runtime),
                preferences,
                storage);

            var copied = await service.CopyAsync("source", "copy", "Copy");

            Assert.True(copied.Succeeded);
            var copiedManifest = Assert.IsType<InstanceManifest>(await service.GetManifestAsync("copy"));
            Assert.Equal("copy", copiedManifest.Id);
            Assert.True(Directory.Exists(Path.Combine(root, "game", "versions", "copy")));
            Assert.True(File.Exists(Path.Combine(root, "game", "versions", "copy", "copy.json")));
            Assert.False(File.Exists(Path.Combine(root, "game", "versions", "copy", "source.json")));

            var deleted = await service.DeleteAsync("copy");

            Assert.True(deleted.Succeeded);
            Assert.False(Directory.Exists(Path.Combine(root, "game", "versions", "copy")));
            Assert.False((await preferences.LoadAsync()).ContainsKey("copy"));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    private static LocalInstanceStorage CreateStorage(string root)
    {
        return new LocalInstanceStorage(new PortableLauncherPaths(
            Path.Combine(root, "game"),
            Path.Combine(root, "launcher-data")));
    }

    private static string CreateRoot()
        => Path.Combine(Path.GetTempPath(), $"slc-instance-service-{Guid.NewGuid():N}");

    private static void DeleteRoot(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }

    private sealed class InMemoryPreferencesStore : IVersionPreferencesStore
    {
        private Dictionary<string, VersionPreference> _preferences =
            new(StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlyDictionary<string, VersionPreference>> LoadAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<string, VersionPreference>>(
                new Dictionary<string, VersionPreference>(_preferences, StringComparer.OrdinalIgnoreCase));

        public Task SaveAsync(
            IReadOnlyDictionary<string, VersionPreference> preferences,
            CancellationToken cancellationToken = default)
        {
            _preferences = new Dictionary<string, VersionPreference>(
                preferences,
                StringComparer.OrdinalIgnoreCase);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRuntime(params GameVersionDescriptor[] versions) : IGameRuntime
    {
        public event EventHandler<GameExitNotification>? GameExited
        {
            add { }
            remove { }
        }

        public string RootPath => @"C:\Minecraft";

        public Task<GameRuntimeSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new GameRuntimeSnapshot(
                RootPath,
                versions.Select(version => version.Id).ToArray(),
                true));

        public Task<IReadOnlyList<GameVersionDescriptor>> GetVersionsAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<GameVersionDescriptor>>(versions);

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
}
