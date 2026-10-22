using SlcMinecraftLaunch.Infrastructure.Configuration;

namespace SlcMinecraftLaunch.Tests;

public sealed class PortableLauncherPathsTests
{
    [Fact]
    public void Resolve_UsesPortableMinecraftDirectoryByDefault()
    {
        var executableDirectory = Path.Combine(Path.GetTempPath(), "slc-launcher");

        var paths = PortableLauncherPaths.Resolve(null, executableDirectory);

        Assert.Equal(
            Path.GetFullPath(Path.Combine(executableDirectory, ".minecraft")),
            paths.GameRoot);
        Assert.Equal(
            Path.GetFullPath(Path.Combine(executableDirectory, "launcher-data")),
            paths.DataRoot);
    }

    [Fact]
    public void Resolve_HonorsExplicitGameRoot()
    {
        var executableDirectory = Path.Combine(Path.GetTempPath(), "slc-launcher");
        var gameRoot = Path.Combine(Path.GetTempPath(), "custom-minecraft");

        var paths = PortableLauncherPaths.Resolve(gameRoot, executableDirectory);

        Assert.Equal(Path.GetFullPath(gameRoot), paths.GameRoot);
    }
}
