using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Tests;

public sealed class OperationProgressTests
{
    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0.5, 0.5)]
    [InlineData(2, 1)]
    public void NormalizeFraction_ClampsToValidRange(double input, double expected)
    {
        Assert.Equal(expected, OperationProgress.NormalizeFraction(input));
    }
}
