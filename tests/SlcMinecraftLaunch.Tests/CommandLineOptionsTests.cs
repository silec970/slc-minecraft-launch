using SlcMinecraftLaunch.Application;

namespace SlcMinecraftLaunch.Tests;

public sealed class CommandLineOptionsTests
{
    [Fact]
    public void Parse_ReadsSmokeTestArguments()
    {
        var options = CommandLineOptions.Parse(
            ["--smoke-test", "--version", "1.21.4", "--game-root", @"C:\Games\MC", "--java", @"C:\Java\java.exe"]);

        Assert.True(options.SmokeTest);
        Assert.Equal("1.21.4", options.Version);
        Assert.Equal(@"C:\Games\MC", options.GameRoot);
        Assert.Equal(@"C:\Java\java.exe", options.JavaPath);
    }
}

