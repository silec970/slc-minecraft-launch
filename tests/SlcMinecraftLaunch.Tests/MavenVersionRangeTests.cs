using SlcMinecraftLaunch.Application;

namespace SlcMinecraftLaunch.Tests;

public sealed class MavenVersionRangeTests
{
    [Theory]
    [InlineData(">=2.0.0", "2.1.0", true)]
    [InlineData("[1.0,2.0)", "2.0", false)]
    [InlineData("[1.0,2.0]", "2.0", true)]
    [InlineData(">=1.0 <2.0", "1.9.9", true)]
    [InlineData("1.2.3", "1.2.3", true)]
    [InlineData("<1.0 || >=3.0", "3.2", true)]
    public void IsSatisfied_HandlesMavenAndOrRanges(
        string range,
        string version,
        bool expected)
    {
        Assert.Equal(expected, MavenVersionRange.IsSatisfied(range, version));
    }

    [Fact]
    public void Intersection_DetectsDisjointAndOverlappingRanges()
    {
        Assert.False(MavenVersionRange.HaveIntersection("[1.0,2.0)", "[2.0,3.0]"));
        Assert.True(MavenVersionRange.HaveIntersection("[1.0,2.0]", "[2.0,3.0]"));
        Assert.True(MavenVersionRange.HaveIntersection(">=2.0", "<3.0"));
    }
}
