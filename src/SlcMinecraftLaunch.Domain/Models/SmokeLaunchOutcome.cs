namespace SlcMinecraftLaunch.Domain.Models;

public sealed record SmokeLaunchOutcome(
    bool Succeeded,
    string MessageKey,
    int? ExitCode,
    string? ErrorDetail = null,
    IReadOnlyList<string>? Warnings = null);

