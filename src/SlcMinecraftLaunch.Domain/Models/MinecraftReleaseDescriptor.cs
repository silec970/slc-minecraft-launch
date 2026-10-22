namespace SlcMinecraftLaunch.Domain.Models;

public sealed record MinecraftReleaseDescriptor(
    string Id,
    string Type,
    DateTimeOffset ReleaseTime,
    bool IsLatestRelease,
    bool IsLatestSnapshot);

public sealed record VanillaInstallationOutcome(
    bool Succeeded,
    string MessageKey,
    string VersionId,
    string? ErrorDetail = null);

