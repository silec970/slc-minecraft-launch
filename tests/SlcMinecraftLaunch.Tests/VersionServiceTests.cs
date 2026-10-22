using SlcMinecraftLaunch.Application;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;
using SlcMinecraftLaunch.Infrastructure.Persistence;

namespace SlcMinecraftLaunch.Tests;

public sealed class VersionServiceTests
{
    [Fact]
    public async Task GetCatalog_GroupsVersionsAndPlacesFavoritesFirst()
    {
        var runtime = new FakeRuntime(
            CreateVersion("1.21.4", LoaderKind.Vanilla),
            CreateVersion("1.20.1-forge-47.2.0", LoaderKind.Forge),
            CreateVersion("1.21.1-neoforge-21.1.80", LoaderKind.NeoForge),
            CreateVersion("1.21.4-fabric-0.16.9", LoaderKind.Fabric));
        var store = new InMemoryPreferencesStore(
            new Dictionary<string, VersionPreference>
            {
                ["1.20.1-forge-47.2.0"] = new(IsFavorite: true, IsIsolated: false)
            });
        var service = new VersionService(new FakeRuntimeProvider(runtime), store);

        var catalog = await service.GetCatalogAsync();

        Assert.Equal(
            [LoaderKind.Vanilla, LoaderKind.NeoForge, LoaderKind.Forge, LoaderKind.Fabric],
            catalog.Groups.Select(group => group.LoaderKind));
        var forge = catalog.AllVersions.Single(entry => entry.Version.Id == "1.20.1-forge-47.2.0");
        Assert.True(forge.IsFavorite);
        Assert.False(forge.IsIsolated);
        Assert.All(
            catalog.AllVersions.Where(entry => entry.Version.Id != "1.20.1-forge-47.2.0"),
            entry => Assert.True(entry.IsIsolated));
    }

    [Fact]
    public async Task SetFavoriteAndIsolation_PersistIndependentPreferences()
    {
        var runtime = new FakeRuntime(CreateVersion("1.21.4", LoaderKind.Vanilla));
        var store = new InMemoryPreferencesStore();
        var service = new VersionService(new FakeRuntimeProvider(runtime), store);

        await service.SetFavoriteAsync("1.21.4", true);
        await service.SetIsolationAsync("1.21.4", false);

        var catalog = await service.GetCatalogAsync();
        var entry = Assert.Single(catalog.AllVersions);
        Assert.True(entry.IsFavorite);
        Assert.False(entry.IsIsolated);
    }

    [Fact]
    public async Task SetFavorite_RejectsUnknownVersion()
    {
        var runtime = new FakeRuntime(CreateVersion("1.21.4", LoaderKind.Vanilla));
        var service = new VersionService(
            new FakeRuntimeProvider(runtime),
            new InMemoryPreferencesStore());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SetFavoriteAsync("missing", true));
    }

    [Fact]
    public async Task JsonStore_RoundTripsPreferences()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"slc-version-store-{Guid.NewGuid():N}");
        try
        {
            var store = new JsonVersionPreferencesStore(tempRoot);
            await store.SaveAsync(new Dictionary<string, VersionPreference>
            {
                ["1.21.4"] = new(IsFavorite: true, IsIsolated: false)
            });

            var loaded = await store.LoadAsync();

            Assert.True(loaded["1.21.4"].IsFavorite);
            Assert.False(loaded["1.21.4"].IsIsolated);
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }

    private static GameVersionDescriptor CreateVersion(string id, LoaderKind loaderKind)
    {
        return new GameVersionDescriptor(
            id,
            id,
            id.Split('-')[0],
            loaderKind,
            null,
            21,
            "java-runtime-delta",
            false);
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
        {
            return Task.FromResult(new GameRuntimeSnapshot(
                RootPath,
                versions.Select(version => version.Id).ToArray(),
                true));
        }

        public Task<IReadOnlyList<GameVersionDescriptor>> GetVersionsAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<GameVersionDescriptor>>(versions);
        }

        public Task<ResourceCompletionOutcome> CompleteResourcesAsync(
            ResourceCompletionRequest request,
            IProgress<OperationProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ResourceCompletionOutcome(true, "Download.Completed"));
        }

        public Task<ResourceCompletionOutcome> DownloadModAsync(
            ModDownloadRequest request,
            IProgress<OperationProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ResourceCompletionOutcome(true, "Mods.Downloaded"));
        }

        public Task<GameLaunchOutcome> LaunchOfflineAsync(
            GameLaunchRequest request,
            IProgress<OperationProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new GameLaunchOutcome(true, "Launch.Started"));
        }

        public Task<SmokeLaunchOutcome> LaunchSmokeAsync(
            SmokeLaunchRequest request,
            IProgress<OperationProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeRuntimeProvider(IGameRuntime runtime) : IGameRuntimeProvider
    {
        public Task<IGameRuntime> GetAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(runtime);
        }
    }

    private sealed class InMemoryPreferencesStore(
        Dictionary<string, VersionPreference>? initial = null) : IVersionPreferencesStore
    {
        private readonly Dictionary<string, VersionPreference> _preferences =
            initial ?? new Dictionary<string, VersionPreference>(StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlyDictionary<string, VersionPreference>> LoadAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyDictionary<string, VersionPreference>>(
                new Dictionary<string, VersionPreference>(_preferences, StringComparer.OrdinalIgnoreCase));
        }

        public Task SaveAsync(
            IReadOnlyDictionary<string, VersionPreference> preferences,
            CancellationToken cancellationToken = default)
        {
            _preferences.Clear();
            foreach (var preference in preferences)
            {
                _preferences[preference.Key] = preference.Value;
            }

            return Task.CompletedTask;
        }
    }
}
