using SlcMinecraftLaunch.Infrastructure.Mods;

namespace SlcMinecraftLaunch.Tests;

public sealed class SafeRemoteFileNameTests
{
    [Theory]
    [InlineData("../../evil.jar", "evil.jar")]
    [InlineData("../folder/mod.jar", "mod.jar")]
    [InlineData("bad\u0001name", "bad_name.jar")]
    [InlineData("", "mod.jar")]
    [InlineData("..", "mod.jar")]
    public void Normalize_RemovesPathAndUnsafeCharacters(string input, string expected)
    {
        Assert.Equal(expected, SafeRemoteFileName.Normalize(input));
    }
}
