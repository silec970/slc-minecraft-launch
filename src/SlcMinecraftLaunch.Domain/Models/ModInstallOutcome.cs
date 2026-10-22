namespace SlcMinecraftLaunch.Domain.Models;

public sealed record ModInstallOutcome(
    bool Succeeded,
    string MessageKey,
    string? InstalledFilePath = null,
    string? ErrorDetail = null,
    IReadOnlyList<ModConflict>? Conflicts = null,
    IReadOnlyList<string>? Warnings = null);

