namespace SlcMinecraftLaunch.Domain.Models;

public sealed record OperationProgress(
    SmokeTestStage Stage,
    string MessageKey,
    double Fraction,
    long? BytesReceived = null,
    long? TotalBytes = null,
    double? BytesPerSecond = null,
    string? Detail = null)
{
    public static double NormalizeFraction(double fraction)
    {
        if (double.IsNaN(fraction))
        {
            return 0;
        }

        return Math.Clamp(fraction, 0, 1);
    }
}

