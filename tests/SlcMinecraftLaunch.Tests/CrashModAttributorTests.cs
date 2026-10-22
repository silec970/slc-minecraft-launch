using SlcMinecraftLaunch.Domain.Models;
using SlcMinecraftLaunch.Infrastructure.Crash;

namespace SlcMinecraftLaunch.Tests;

public sealed class CrashModAttributorTests
{
    [Fact]
    public async Task Attribute_FindsProvableSingleModFile()
    {
        var root = Path.Combine(Path.GetTempPath(), $"slc-crash-mod-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "mods"));
            await File.WriteAllTextAsync(Path.Combine(root, "mods", "examplemod.jar"), "jar");
            var logPath = Path.Combine(root, "latest.log");
            await File.WriteAllTextAsync(
                logPath,
                "java.lang.RuntimeException: failed\n\tat app//examplemod.Example(examplemod.jar:42)");

            var insight = await CrashModAttributor.AttributeAsync(
                CreateInsight(logPath),
                new CrashAnalysisRequest(root, "1.21.4", false));

            Assert.Equal("Crash.ModAttribution.Single", insight.AttributionKey);
            Assert.Equal(["examplemod.jar"], insight.AffectedModFiles);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task Attribute_DoesNotGuessWhenEvidenceIsMissing()
    {
        var root = Path.Combine(Path.GetTempPath(), $"slc-crash-mod-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "mods"));
            await File.WriteAllTextAsync(Path.Combine(root, "mods", "examplemod.jar"), "jar");
            var logPath = Path.Combine(root, "latest.log");
            await File.WriteAllTextAsync(logPath, "java.lang.OutOfMemoryError");

            var insight = await CrashModAttributor.AttributeAsync(
                CreateInsight(logPath),
                new CrashAnalysisRequest(root, "1.21.4", false));

            Assert.Equal("Crash.ModAttribution.Uncertain", insight.AttributionKey);
            Assert.Empty(insight.AffectedModFiles ?? []);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    private static CrashInsight CreateInsight(string logPath)
    {
        return new CrashInsight(
            "ModCausedGameCrash",
            CrashCategory.Mod,
            CrashSeverity.Error,
            "Crash.Category.Mod.Title",
            "Crash.Category.Mod.Suggestion",
            logPath);
    }

    private static void DeleteRoot(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}
