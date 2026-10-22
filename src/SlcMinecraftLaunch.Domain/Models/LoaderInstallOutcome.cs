namespace SlcMinecraftLaunch.Domain.Models;

public sealed record LoaderInstallOutcome(
    bool Succeeded,
    string MessageKey,
    string? InstalledVersionId = null,
    string? ErrorDetail = null,
    IReadOnlyList<string>? Warnings = null);

