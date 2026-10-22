using SlcMinecraftLaunch.Application;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Tests;

public sealed class ModConflictAnalyzerTests
{
    [Fact]
    public void Analyze_DetectsDuplicateAndMissingDependency()
    {
        var mods = new[]
        {
            CreateEntry("a.jar", "example", [new ModRequirement("fabric-api", true)]),
            CreateEntry("b.jar", "example", []),
            CreateEntry("c.jar", "other", [])
        };
        var analyzer = new ModConflictAnalyzer();

        var conflicts = analyzer.Analyze(mods);

        Assert.Contains(conflicts, conflict => conflict.Kind == ModConflictKind.DuplicateModId);
        Assert.Contains(conflicts, conflict => conflict.Kind == ModConflictKind.MissingDependency);
    }

    [Fact]
    public void Analyze_DetectsConfiguredKnownIncompatibility()
    {
        var analyzer = new ModConflictAnalyzer(
        [
            new KnownModConflict("first", "second", "Mods.Conflict.Test")
        ]);

        var conflicts = analyzer.Analyze(
        [
            CreateEntry("first.jar", "first", []),
            CreateEntry("second.jar", "second", [])
        ]);

        Assert.Contains(
            conflicts,
            conflict => conflict.Kind == ModConflictKind.KnownIncompatibility);
    }

    [Fact]
    public void Analyze_DetectsVersionMismatchAndDiamondConflict()
    {
        var analyzer = new ModConflictAnalyzer();
        var mods = new[]
        {
            CreateEntry("parent-a.jar", "parent-a", [new ModRequirement("library", true, "[1.0,2.0)")]),
            CreateEntry("parent-b.jar", "parent-b", [new ModRequirement("library", true, "[2.0,3.0]")]),
            CreateEntry("library.jar", "library", [], "1.5.0")
        };

        var conflicts = analyzer.Analyze(mods);

        Assert.Contains(
            conflicts,
            conflict => conflict.Kind == ModConflictKind.DiamondDependencyConflict);
        Assert.Contains(
            conflicts,
            conflict => conflict.Kind == ModConflictKind.IncompatibleDependencyVersion);
    }

    private static ModEntry CreateEntry(
        string fileName,
        string modId,
        IReadOnlyList<ModRequirement> requirements,
        string version = "1.0")
    {
        return new ModEntry(
            Path.Combine(Path.GetTempPath(), fileName),
            fileName,
            new ModMetadata(modId, modId, version, requirements),
            100);
    }
}

