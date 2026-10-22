using SlcMinecraftLaunch.Domain.Models;
using SlcMinecraftLaunch.Infrastructure.Crash;

namespace SlcMinecraftLaunch.Tests;

public sealed class CrashInsightClassifierTests
{
    [Theory]
    [InlineData("NoEnoughMemory", CrashCategory.Memory)]
    [InlineData("UnsupportedJavaVersion", CrashCategory.Java)]
    [InlineData("UnsupportedNvDriver", CrashCategory.Gpu)]
    [InlineData("ModCausedGameCrash", CrashCategory.Mod)]
    [InlineData("ForgeError", CrashCategory.Loader)]
    [InlineData("FileIsInUse", CrashCategory.Configuration)]
    [InlineData("Other", CrashCategory.Other)]
    public void Classify_MapsKnownCategories(string cause, CrashCategory expected)
    {
        var insight = CrashInsightClassifier.Classify(cause);

        Assert.Equal(expected, insight.Category);
        Assert.StartsWith("Crash.Category.", insight.MessageKey);
        Assert.StartsWith("Crash.Category.", insight.SuggestionKey);
    }
}

