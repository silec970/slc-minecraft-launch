namespace SlcMinecraftLaunch.Infrastructure.Configuration;

public sealed record PortableLauncherPaths(string GameRoot, string DataRoot)
{
    public static PortableLauncherPaths Resolve(string? configuredGameRoot, string? executableDirectory = null)
    {
        var applicationDirectory = Path.GetFullPath(executableDirectory ?? AppContext.BaseDirectory);
        var gameRoot = string.IsNullOrWhiteSpace(configuredGameRoot)
            ? Path.Combine(applicationDirectory, ".minecraft")
            : Path.GetFullPath(configuredGameRoot);
        var dataRoot = Path.Combine(applicationDirectory, "launcher-data");

        return new PortableLauncherPaths(
            Path.TrimEndingDirectorySeparator(gameRoot),
            Path.TrimEndingDirectorySeparator(dataRoot));
    }
}

