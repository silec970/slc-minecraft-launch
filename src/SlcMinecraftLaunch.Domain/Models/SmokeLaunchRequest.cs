namespace SlcMinecraftLaunch.Domain.Models;

public sealed record SmokeLaunchRequest(
    string Version,
    string JavaPath,
    string OfflineUsername,
    TimeSpan HealthCheckDuration);

