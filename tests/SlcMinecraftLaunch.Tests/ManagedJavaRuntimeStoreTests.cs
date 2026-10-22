using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SlcMinecraftLaunch.Infrastructure.Configuration;
using SlcMinecraftLaunch.Infrastructure.Java;

namespace SlcMinecraftLaunch.Tests;

public sealed class ManagedJavaRuntimeStoreTests
{
    [Fact]
    public async Task DiscoverAndReverify_DetectsTamperedManagedRuntime()
    {
        var root = Path.Combine(Path.GetTempPath(), $"slc-java-store-{Guid.NewGuid():N}");
        try
        {
            var gameRoot = Path.Combine(root, "game");
            var runtimeDirectory = Path.Combine(
                gameRoot,
                "runtimes",
                "mojang",
                "java-runtime-delta",
                "21");
            var executablePath = Path.Combine(runtimeDirectory, "bin", "java.exe");
            await CreateManagedRuntimeAsync(runtimeDirectory, executablePath);
            var store = CreateStore(gameRoot, root);

            var discovered = Assert.Single(await store.DiscoverAsync());
            Assert.Equal("Mojang", discovered.Source.ToString());
            Assert.Equal(21, discovered.MajorVersion);
            Assert.Equal("Recorded", discovered.IntegrityStatus.ToString());

            var verified = await store.ReverifyAsync(executablePath);
            Assert.NotNull(verified);
            Assert.Equal("Verified", verified.IntegrityStatus.ToString());

            await File.WriteAllTextAsync(executablePath, "tampered");
            var invalid = await store.ReverifyAsync(executablePath);
            Assert.NotNull(invalid);
            Assert.Equal("Invalid", invalid.IntegrityStatus.ToString());
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
    public async Task DeleteManaged_OnlyDeletesValidatedRuntimeDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"slc-java-delete-{Guid.NewGuid():N}");
        try
        {
            var gameRoot = Path.Combine(root, "game");
            var runtimeDirectory = Path.Combine(
                gameRoot,
                "runtimes",
                "adoptium",
                "temurin-21-x64");
            var executablePath = Path.Combine(runtimeDirectory, "bin", "java.exe");
            await CreateManagedRuntimeAsync(
                runtimeDirectory,
                executablePath,
                source: "adoptium");
            var store = CreateStore(gameRoot, root);
            var outsidePath = Path.Combine(root, "outside", "bin", "java.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(outsidePath)!);
            await File.WriteAllTextAsync(outsidePath, "outside");

            Assert.False(await store.DeleteManagedAsync(outsidePath));
            Assert.True(File.Exists(outsidePath));
            Assert.True(await store.DeleteManagedAsync(executablePath));
            Assert.False(Directory.Exists(runtimeDirectory));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    private static ManagedJavaRuntimeStore CreateStore(string gameRoot, string dataRoot)
    {
        return new ManagedJavaRuntimeStore(
            new PortableLauncherPaths(gameRoot, dataRoot),
            new JavaRuntimeIntegrityVerifier());
    }

    private static async Task CreateManagedRuntimeAsync(
        string runtimeDirectory,
        string executablePath,
        string source = "mojang")
    {
        Directory.CreateDirectory(Path.GetDirectoryName(executablePath)!);
        await File.WriteAllTextAsync(executablePath, "fake-java");
        await File.WriteAllTextAsync(
            Path.Combine(runtimeDirectory, ".slc-ready"),
            DateTimeOffset.UtcNow.ToString("O"));
        var sha256 = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes("fake-java")));
        await File.WriteAllTextAsync(
            Path.Combine(runtimeDirectory, ".integrity.json"),
            JsonSerializer.Serialize(new
            {
                schemaVersion = 2,
                source,
                component = source == "mojang" ? "java-runtime-delta" : "temurin-21",
                requiredMajorVersion = 21,
                officialSha1 = source == "mojang"
                    ? new Dictionary<string, string> { ["bin/java.exe"] = "ABC123" }
                    : new Dictionary<string, string>(),
                officialArchiveSha256 = source == "adoptium" ? "DEF456" : null,
                files = new Dictionary<string, string> { ["bin/java.exe"] = sha256 }
            }));
    }
}
