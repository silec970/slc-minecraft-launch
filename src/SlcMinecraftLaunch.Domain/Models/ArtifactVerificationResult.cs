namespace SlcMinecraftLaunch.Domain.Models;

public enum ArtifactVerificationStatus
{
    Verified,
    Missing,
    Mismatch
}

public sealed record ArtifactVerificationResult(
    ArtifactVerificationStatus Status,
    string Algorithm,
    string? ExpectedHash,
    string? ActualHash,
    string? Detail = null);

