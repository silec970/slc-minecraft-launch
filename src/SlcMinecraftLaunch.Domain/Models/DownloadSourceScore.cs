namespace SlcMinecraftLaunch.Domain.Models;

public sealed record DownloadSourceScore(
    string SourceRoot,
    int Score,
    int SuccessCount,
    int FailureCount,
    int ConsecutiveSuccesses,
    DateTimeOffset UpdatedAt);

public sealed record DownloadSourceObservation(
    string SourceRoot,
    int SuccessCount,
    int FailureCount);
