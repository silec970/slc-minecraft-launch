namespace SlcMinecraftLaunch.Domain.Models;

public sealed record SmokeTestResult(
    bool Succeeded,
    string TargetVersion,
    SmokeTestStage FinalStage,
    string MessageKey,
    TimeSpan Duration,
    int? ExitCode,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<OperationProgress> Progress,
    string? ErrorDetail = null);

