using System.IO.Compression;
using System.Text;
using SlcMinecraftLaunch.Domain.Models;
using SlcMinecraftLaunch.Infrastructure.Mods;

namespace SlcMinecraftLaunch.Tests;

public sealed class LocalModStoreTests
{
    [Fact]
    public async Task ImportAndScan_ReadsFabricMetadata()
    {
        var root = Path.Combine(Path.GetTempPath(), $"slc-mod-{Guid.NewGuid():N}");
        var source = Path.Combine(root, "source.jar");
        Directory.CreateDirectory(root);

        try
        {
            await CreateFabricJarAsync(source);
            var store = new LocalModStore();

            var imported = await store.ImportAsync(root, "1.21.4", true, source);
            var scanned = await store.ScanAsync(root, "1.21.4", true);

            Assert.Equal("examplemod", imported.Metadata.ModId);
            var entry = Assert.Single(scanned);
            Assert.Equal("Example Mod", entry.Metadata.Name);
            Assert.Contains(
                entry.Metadata.Requirements,
                requirement => requirement.ModId == "fabric-api");
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public async Task Import_RejectsNonModJar()
    {
        var root = Path.Combine(Path.GetTempPath(), $"slc-mod-{Guid.NewGuid():N}");
        var source = Path.Combine(root, "invalid.jar");
        Directory.CreateDirectory(root);

        try
        {
            await using (var stream = File.Create(source))
            await using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false))
            {
                var entry = archive.CreateEntry("readme.txt");
                await using var writer = new StreamWriter(entry.Open());
                await writer.WriteAsync("not a mod");
            }

            var store = new LocalModStore();
            await Assert.ThrowsAsync<InvalidDataException>(
                () => store.ImportAsync(root, "1.21.4", true, source));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    private static async Task CreateFabricJarAsync(string path)
    {
        await using var stream = File.Create(path);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false);
        var entry = archive.CreateEntry("fabric.mod.json");
        await using var entryStream = entry.Open();
        var json = """
                   {
                     "id": "examplemod",
                     "name": "Example Mod",
                     "version": "1.0.0",
                     "depends": {
                       "fabric-api": "*"
                     }
                   }
                   """;
        await entryStream.WriteAsync(Encoding.UTF8.GetBytes(json));
    }
}
