using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Infrastructure.Crash;

public static class CrashInsightClassifier
{
    private static readonly HashSet<string> MemoryCauses =
    [
        "NoEnoughMemory",
        "NoEnoughMemory32"
    ];

    private static readonly HashSet<string> JavaCauses =
    [
        "JavaVersionTooHigh",
        "UnsupportedJavaVersion",
        "JdkUse",
        "OpenJ9Use",
        "LegacyForgeDoesNotSupportNewerJava"
    ];

    private static readonly HashSet<string> GpuCauses =
    [
        "UnableToSetPixelFormat",
        "UnsupportedIntelDriver",
        "UnsupportedAmdDriver",
        "UnsupportedNvDriver",
        "GpuDoesNotSupportOpenGl",
        "OpenGl1282Error",
        "TextureTooLargeOrLowEndGpu"
    ];

    private static readonly HashSet<string> ModCauses =
    [
        "ModCausedGameCrash",
        "DecompressedMod",
        "IncorrectModConfig",
        "ModMixinFailed",
        "ModInitFailed",
        "ModIdExceeded",
        "DuplicateMod",
        "ContentValidationFailed",
        "BlockCausedGameCrash",
        "EntityCausedGameCrash"
    ];

    private static readonly HashSet<string> LoaderCauses =
    [
        "FabricError",
        "FabricErrorWithSolution",
        "ForgeError",
        "MultipleForgeInVersionJson",
        "IncompatibleForgeAndOptifine",
        "ModLoaderError",
        "FailedToLoadWorldBecauseOptiFine"
    ];

    public static CrashInsight Classify(
        string causeCode,
        string? source = null,
        IReadOnlyList<string>? details = null)
    {
        var category = MemoryCauses.Contains(causeCode)
            ? CrashCategory.Memory
            : JavaCauses.Contains(causeCode)
                ? CrashCategory.Java
                : GpuCauses.Contains(causeCode)
                    ? CrashCategory.Gpu
                    : ModCauses.Contains(causeCode)
                        ? CrashCategory.Mod
                        : LoaderCauses.Contains(causeCode)
                            ? CrashCategory.Loader
                            : causeCode is "IncorrectPathEncodingOrMainClassNotFound" or "FileIsInUse"
                                or "LogFileNotFound"
                                ? CrashCategory.Configuration
                                : CrashCategory.Other;

        var severity = category switch
        {
            CrashCategory.Memory => CrashSeverity.Critical,
            CrashCategory.Java => CrashSeverity.Error,
            CrashCategory.Gpu => CrashSeverity.Error,
            CrashCategory.Mod => CrashSeverity.Error,
            CrashCategory.Loader => CrashSeverity.Error,
            CrashCategory.Configuration => CrashSeverity.Warning,
            _ => CrashSeverity.Info
        };

        return new CrashInsight(
            causeCode,
            category,
            severity,
            $"Crash.Category.{category}.Title",
            $"Crash.Category.{category}.Suggestion",
            source,
            details);
    }
}

