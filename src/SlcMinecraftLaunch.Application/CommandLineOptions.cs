namespace SlcMinecraftLaunch.Application;

public sealed record CommandLineOptions(
    bool SmokeTest,
    string? GameRoot,
    string? Version,
    string? JavaPath)
{
    public static CommandLineOptions Parse(IReadOnlyList<string> args)
    {
        var smokeTest = false;
        string? gameRoot = null;
        string? version = null;
        string? javaPath = null;

        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            switch (argument)
            {
                case "--smoke-test":
                    smokeTest = true;
                    break;
                case "--game-root" when index + 1 < args.Count:
                    gameRoot = args[++index];
                    break;
                case "--version" when index + 1 < args.Count:
                    version = args[++index];
                    break;
                case "--java" when index + 1 < args.Count:
                    javaPath = args[++index];
                    break;
            }
        }

        return new CommandLineOptions(smokeTest, gameRoot, version, javaPath);
    }
}

