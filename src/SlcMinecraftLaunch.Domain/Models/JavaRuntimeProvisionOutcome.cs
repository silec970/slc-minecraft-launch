namespace SlcMinecraftLaunch.Domain.Models;

public sealed record JavaRuntimeProvisionOutcome(
    bool Succeeded,
    string? ExecutablePath,
    string MessageKey,
    string? ErrorDetail = null);

