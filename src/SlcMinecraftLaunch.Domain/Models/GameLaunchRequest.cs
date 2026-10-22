namespace SlcMinecraftLaunch.Domain.Models;

public sealed record GameLaunchRequest(
    string VersionId,
    string PlayerName,
    string JavaExecutablePath,
    bool VersionIsolation,
    InstanceLaunchSettings? InstanceSettings = null);

public sealed record GameLaunchOutcome(
    bool Succeeded,
    string MessageKey,
    string? ErrorDetail = null);

