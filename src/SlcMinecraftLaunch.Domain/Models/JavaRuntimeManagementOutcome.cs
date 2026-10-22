namespace SlcMinecraftLaunch.Domain.Models;

public sealed record JavaRuntimeManagementOutcome(
    bool Succeeded,
    string MessageKey,
    string? ErrorDetail = null);
