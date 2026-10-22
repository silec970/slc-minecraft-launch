using System.IO.Compression;
using System.Text;
using SlcMinecraftLaunch.Domain.Models;
using SlcMinecraftLaunch.Infrastructure.Crash;

namespace SlcMinecraftLaunch.Tests;

public sealed class CrashAnalysisIntegrationTests
{
    [Fact]
    public async Task Backend_UsesProjBobcatAnalysisAndAttributesKnownModFilename()
    {
        var root = Path.Combine(Path.GetTempPath(), $"slc-crash-analysis-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "logs"));
            Directory.CreateDirectory(Path.Combine(root, "mods"));
            await File.WriteAllTextAsync(
                Path.Combine(root, "logs", "latest.log"),
                "[12:00:00] [main/ERROR]: Caught exception from examplemod (examplemod)");
            await CreateFabricJarAsync(Path.Combine(root, "mods", "examplemod.jar"), "examplemod");
            var backend = new ProjBobcatCrashAnalysisBackend();

            var insights = await backend.AnalyzeAsync(new CrashAnalysisRequest(
                root,
                "1.21.4",
                false));

            var modInsight = Assert.Single(
                insights,
                insight => insight.Category == CrashCategory.Mod);
            Assert.Equal("Crash.ModAttribution.Single", modInsight.AttributionKey);
            Assert.Contains("examplemod.jar", modInsight.AffectedModFiles ?? []);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    private static async Task CreateFabricJarAsync(string path, string modId)
    {
        await using var stream = File.Create(path);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
        var entry = archive.CreateEntry("fabric.mod.json");
        await using var output = new StreamWriter(
            entry.Open(),
            new UTF8Encoding(false),
            leaveOpen: false);
        await output.WriteAsync(
            $$"""{"schemaVersion":1,"id":"{{modId}}","version":"1.0.0","name":"Example"}""");
    }
}
