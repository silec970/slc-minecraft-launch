namespace SlcMinecraftLaunch.Domain.Models;

public sealed record SmokeTestRequest(
    string RootPath,
    string TargetVersion = "1.21.4",
    string? ExplicitJavaPath = null,
    string OfflineUsername = "SmokeTestBot",
    TimeSpan? HealthCheckDuration = null)
{
    public TimeSpan EffectiveHealthCheckDuration =>
        HealthCheckDuration ?? TimeSpan.FromSeconds(5);
}

