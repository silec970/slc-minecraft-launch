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
        Assert.False(options.InstallLoader);
        Assert.Equal("1.21.4", options.Version);
        Assert.Equal(@"C:\Games\MC", options.GameRoot);
        Assert.Equal(@"C:\Java\java.exe", options.JavaPath);
    }

    [Fact]
    public void Parse_ReadsLoaderInstallationArguments()
    {
        var options = CommandLineOptions.Parse(
            ["--install-loader", "--loader", "fabric", "--version", "1.21.4", "--loader-version", "0.16.9"]);

        Assert.True(options.InstallLoader);
        Assert.Equal("fabric", options.Loader);
        Assert.Equal("0.16.9", options.LoaderVersion);
    }

    [Fact]
    public void Parse_ReadsJavaProvisioningArguments()
    {
        var options = CommandLineOptions.Parse(
            ["--provision-java", "--java-major", "21"]);

        Assert.True(options.ProvisionJava);
        Assert.Equal(21, options.JavaMajor);
    }
}

