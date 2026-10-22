using System.IO.Compression;
using System.Text;
using System.Text.Json;
using SlcMinecraftLaunch.Application;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;
using SlcMinecraftLaunch.Infrastructure.Configuration;
using SlcMinecraftLaunch.Infrastructure.Instances;

namespace SlcMinecraftLaunch.Tests;

public sealed class InstanceTransferServiceTests
{
    [Fact]
    public async Task Import_ValidatesMatchingLoaderAndExtractsSelectedData()
    {
        var root = CreateRoot();
        try
        {
            var storage = CreateStorage(root);
            var sourceManifest = LocalInstanceStorageTests.CreateManifest("base-source", false);
            await storage.CreatePlaceholderAsync(sourceManifest);
            await LocalInstanceStorageTests.WriteFileAsync(
                Path.Combine(root, "game", "versions", "base-source", "base-source.json"),
                """{"id":"base-source","type":"release","inheritsFrom":"1.21.4","libraries":[],"downloads":{}}""");
            var runtime = new FakeRuntime(new GameVersionDescriptor(
                "base-source",
                "Base Source",
                "1.21.4",
                LoaderKind.Forge,
                "54.1.5",
                21,
                "java-runtime-delta",
                false));
            var preferences = new InMemoryPreferencesStore();
            var sources = new InMemoryModSourceStore();
            var service = new InstanceTransferService(
                new FakeRuntimeProvider(runtime),
                storage,
                preferences,
                sources);
            var importedManifest = LocalInstanceStorageTests.CreateManifest("imported", false);
            var archivePath = Path.Combine(root, "import.zip");
            CreateImportArchive(
                archivePath,
                importedManifest,
                new Dictionary<string, ModSourceMetadata>
                {
                    ["examplemod"] = new(
                        "examplemod",
                        ModProvider.Modrinth,
                        "example-project",
                        "version-1",
                        [])
                });

            var result = await service.ImportAsync(archivePath);

            Assert.True(result.Succeeded);
            var targetDirectory = Path.Combine(root, "game", "versions", "imported");
            Assert.True(File.Exists(Path.Combine(targetDirectory, "imported.json")));
            Assert.True(File.Exists(Path.Combine(targetDirectory, "mods", "example.jar")));
            Assert.True(File.Exists(Path.Combine(targetDirectory, "config", "test.txt")));
            Assert.True((await preferences.LoadAsync())["imported"].IsIsolated);
            Assert.True((await sources.LoadAsync()).ContainsKey("examplemod"));

            var conflict = await service.ImportAsync(archivePath);
            Assert.False(conflict.Succeeded);
            Assert.Equal("Instances.AlreadyExists", conflict.MessageKey);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task Import_RejectsWhenMatchingMinecraftAndLoaderAreNotInstalled()
    {
        var root = CreateRoot();
        try
        {
            var storage = CreateStorage(root);
            var service = new InstanceTransferService(
                new FakeRuntimeProvider(new FakeRuntime()),
                storage,
                new InMemoryPreferencesStore(),
                new InMemoryModSourceStore());
            var archivePath = Path.Combine(root, "missing-version.zip");
            CreateImportArchive(
                archivePath,
                LocalInstanceStorageTests.CreateManifest("imported", false),
                new Dictionary<string, ModSourceMetadata>());

            var result = await service.ImportAsync(archivePath);

            Assert.False(result.Succeeded);
            Assert.Equal("Instances.ImportVersionMissing", result.MessageKey);
            Assert.False(Directory.Exists(Path.Combine(root, "game", "versions", "imported")));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    private static void CreateImportArchive(
        string archivePath,
        InstanceManifest manifest,
        IReadOnlyDictionary<string, ModSourceMetadata> sources)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(archivePath)!);
        using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
        WriteEntry(
            archive,
            "instance.json",
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
        WriteEntry(
            archive,
            "loader.json",
            JsonSerializer.Serialize(new InstanceLoaderDeclaration(
                manifest.Id,
                manifest.MinecraftVersion,
                manifest.LoaderKind,
                manifest.LoaderVersion)));
        WriteEntry(
            archive,
            "mod-sources.json",
            JsonSerializer.Serialize(sources));
        WriteEntry(archive, "mods/example.jar", "jar");
        WriteEntry(archive, "config/test.txt", "config");
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name);
        using var output = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        output.Write(content);
    }

    private static LocalInstanceStorage CreateStorage(string root)
        => new(new PortableLauncherPaths(
            Path.Combine(root, "game"),
            Path.Combine(root, "launcher-data")));

    private static string CreateRoot()
        => Path.Combine(Path.GetTempPath(), $"slc-instance-transfer-{Guid.NewGuid():N}");

    private static void DeleteRoot(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }

    private sealed class InMemoryPreferencesStore : IVersionPreferencesStore
    {
        private IReadOnlyDictionary<string, VersionPreference> _preferences =
            new Dictionary<string, VersionPreference>(StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlyDictionary<string, VersionPreference>> LoadAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(_preferences);

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

    private sealed class InMemoryModSourceStore : IModSourceStore
    {
        private IReadOnlyDictionary<string, ModSourceMetadata> _sources =
            new Dictionary<string, ModSourceMetadata>(StringComparer.OrdinalIgnoreCase);

        public Task<IReadOnlyDictionary<string, ModSourceMetadata>> LoadAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(_sources);

        public Task SaveAsync(
            IReadOnlyDictionary<string, ModSourceMetadata> sources,
            CancellationToken cancellationToken = default)
        {
            _sources = new Dictionary<string, ModSourceMetadata>(
                sources,
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
