using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using SlcMinecraftLaunch.Domain.Models;
using SlcMinecraftLaunch.Infrastructure.Configuration;
using SlcMinecraftLaunch.Infrastructure.Instances;

namespace SlcMinecraftLaunch.Tests;

public sealed class LocalInstanceStorageTests
{
    [Fact]
    public async Task CreatePlaceholder_WritesOnlyApprovedMinimalVersionFields()
    {
        var root = CreateRoot();
        try
        {
            var storage = CreateStorage(root);
            var manifest = CreateManifest("empty-instance", placeholder: true);

            await storage.CreatePlaceholderAsync(manifest);

            var versionJsonPath = Path.Combine(
                root,
                "game",
                "versions",
                manifest.Id,
                $"{manifest.Id}.json");
            using var document = JsonDocument.Parse(await File.ReadAllTextAsync(versionJsonPath));
            var properties = document.RootElement.EnumerateObject()
                .Select(property => property.Name)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            Assert.Equal(["downloads", "id", "libraries", "type"], properties);
            Assert.Equal(manifest.Id, document.RootElement.GetProperty("id").GetString());
            Assert.False(document.RootElement.TryGetProperty("inheritsFrom", out _));
            Assert.False(document.RootElement.TryGetProperty("client", out _));
            Assert.NotNull(await storage.LoadManifestAsync(manifest.Id));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task Export_HonorsMandatoryDefaultAndOptInPolicies()
    {
        var root = CreateRoot();
        try
        {
            var storage = CreateStorage(root);
            var manifest = CreateManifest("policies", placeholder: false);
            await storage.CreatePlaceholderAsync(manifest);
            var versionDirectory = Path.Combine(root, "game", "versions", manifest.Id);
            await WriteFileAsync(Path.Combine(versionDirectory, "mods", "test.jar"), "jar");
            await WriteFileAsync(Path.Combine(versionDirectory, "config", "settings.txt"), "config");
            await WriteFileAsync(Path.Combine(versionDirectory, "options.txt"), "options");
            await WriteFileAsync(Path.Combine(versionDirectory, "resourcepacks", "pack.zip"), "pack");
            await WriteFileAsync(Path.Combine(versionDirectory, "shaderpacks", "shader.zip"), "shader");
            await WriteFileAsync(Path.Combine(versionDirectory, "datapacks", "data.zip"), "data");
            await WriteFileAsync(Path.Combine(versionDirectory, "scripts", "boot.zs"), "script");
            await WriteFileAsync(Path.Combine(versionDirectory, "structures", "house.nbt"), "structure");
            await WriteFileAsync(Path.Combine(versionDirectory, "saves", "world", "level.dat"), "save");
            await WriteFileAsync(Path.Combine(versionDirectory, "screenshots", "shot.png"), "shot");
            await WriteFileAsync(Path.Combine(versionDirectory, "logs", "latest.log"), "log");
            await WriteFileAsync(Path.Combine(versionDirectory, "servers.dat"), "servers");
            await WriteFileAsync(Path.Combine(root, "launcher-data", "mod-sources.json"), "{}");

            var defaultExport = Path.Combine(root, "default.zip");
            await storage.ExportAsync(
                manifest,
                true,
                new InstanceExportOptions(),
                defaultExport);
            using (var archive = ZipFile.OpenRead(defaultExport))
            {
                var names = archive.Entries.Select(entry => entry.FullName).ToArray();
                Assert.Contains("instance.json", names);
                Assert.Contains("loader.json", names);
                Assert.Contains("mods/test.jar", names);
                Assert.Contains("config/settings.txt", names);
                Assert.Contains("mod-sources.json", names);
                Assert.Contains("options.txt", names);
                Assert.Contains("resourcepacks/pack.zip", names);
                Assert.Contains("shaderpacks/shader.zip", names);
                Assert.Contains("datapacks/data.zip", names);
                Assert.Contains("scripts/boot.zs", names);
                Assert.Contains("structures/house.nbt", names);
                Assert.DoesNotContain(names, name => name.StartsWith("saves/", StringComparison.OrdinalIgnoreCase));
                Assert.DoesNotContain(names, name => name.StartsWith("screenshots/", StringComparison.OrdinalIgnoreCase));
                Assert.DoesNotContain(names, name => name.StartsWith("logs/", StringComparison.OrdinalIgnoreCase));
                Assert.DoesNotContain("servers.dat", names);
            }

            var fullExport = Path.Combine(root, "full.zip");
            await storage.ExportAsync(
                manifest,
                true,
                new InstanceExportOptions(true, true, true, true),
                fullExport);
            using (var archive = ZipFile.OpenRead(fullExport))
            {
                var names = archive.Entries.Select(entry => entry.FullName).ToArray();
                Assert.Contains("saves/world/level.dat", names);
                Assert.Contains("screenshots/shot.png", names);
                Assert.Contains("logs/latest.log", names);
                Assert.Contains("servers.dat", names);
            }
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task Export_NonIsolatedInstanceReadsRootGameData()
    {
        var root = CreateRoot();
        try
        {
            var storage = CreateStorage(root);
            var manifest = CreateManifest("shared", false);
            await storage.CreatePlaceholderAsync(manifest);
            await WriteFileAsync(Path.Combine(root, "game", "mods", "root.jar"), "jar");
            await WriteFileAsync(Path.Combine(root, "game", "config", "root.txt"), "config");
            var exportPath = Path.Combine(root, "shared.zip");

            await storage.ExportAsync(
                manifest,
                false,
                new InstanceExportOptions(),
                exportPath);

            using var archive = ZipFile.OpenRead(exportPath);
            var names = archive.Entries.Select(entry => entry.FullName).ToArray();
            Assert.Contains("mods/root.jar", names);
            Assert.Contains("config/root.txt", names);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("CON")]
    [InlineData("bad:id")]
    public async Task CreatePlaceholder_RejectsUnsafeInstanceIds(string instanceId)
    {
        var root = CreateRoot();
        try
        {
            var storage = CreateStorage(root);

            await Assert.ThrowsAsync<InvalidDataException>(
                () => storage.CreatePlaceholderAsync(CreateManifest(instanceId, true)));
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

    internal static InstanceManifest CreateManifest(string id, bool placeholder)
    {
        var now = DateTimeOffset.UtcNow;
        return new InstanceManifest(
            id,
            id,
            "1.21.4",
            LoaderKind.Forge,
            "54.1.5",
            new InstanceLaunchSettings(),
            placeholder,
            now,
            now);
    }

    internal static async Task WriteFileAsync(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, content);
    }

    private static string CreateRoot()
    {
        return Path.Combine(Path.GetTempPath(), $"slc-instance-storage-{Guid.NewGuid():N}");
    }

    private static void DeleteRoot(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}
