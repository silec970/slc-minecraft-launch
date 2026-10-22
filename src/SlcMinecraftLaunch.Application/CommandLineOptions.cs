namespace SlcMinecraftLaunch.Application;

public sealed record CommandLineOptions(
    bool SmokeTest,
    bool ListVersions,
    bool InstallLoader,
    bool ProvisionJava,
    bool AnalyzeCrash,
    bool SearchMods,
    bool InstallMod,
    bool ScanMods,
    string? GameRoot,
    string? Version,
    string? JavaPath,
    string? Loader,
    string? LoaderVersion,
    int? JavaMajor,
    string? Query,
    string? Provider,
    string? ProjectId)
{
    public static CommandLineOptions Parse(IReadOnlyList<string> args)
    {
        var smokeTest = false;
        var listVersions = false;
        var installLoader = false;
        var provisionJava = false;
        var analyzeCrash = false;
        var searchMods = false;
        var installMod = false;
        var scanMods = false;
        string? gameRoot = null;
        string? version = null;
        string? javaPath = null;
        string? loader = null;
        string? loaderVersion = null;
        int? javaMajor = null;
        string? query = null;
        string? provider = null;
        string? projectId = null;

        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            switch (argument)
            {
                case "--smoke-test":
                    smokeTest = true;
                    break;
                case "--list-versions":
                    listVersions = true;
                    break;
                case "--install-loader":
                    installLoader = true;
                    break;
                case "--provision-java":
                    provisionJava = true;
                    break;
                case "--analyze-crash":
                    analyzeCrash = true;
                    break;
                case "--search-mods":
                    searchMods = true;
                    break;
                case "--install-mod":
                    installMod = true;
                    break;
                case "--scan-mods":
                    scanMods = true;
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
                case "--loader" when index + 1 < args.Count:
                    loader = args[++index];
                    break;
                case "--loader-version" when index + 1 < args.Count:
                    loaderVersion = args[++index];
                    break;
                case "--java-major" when index + 1 < args.Count:
                    javaMajor = int.TryParse(args[++index], out var parsedMajor)
                        ? parsedMajor
                        : null;
                    break;
                case "--query" when index + 1 < args.Count:
                    query = args[++index];
                    break;
                case "--provider" when index + 1 < args.Count:
                    provider = args[++index];
                    break;
                case "--project-id" when index + 1 < args.Count:
                    projectId = args[++index];
                    break;
            }
        }

        return new CommandLineOptions(
            smokeTest,
            listVersions,
            installLoader,
            provisionJava,
            analyzeCrash,
            searchMods,
            installMod,
            scanMods,
            gameRoot,
            version,
            javaPath,
            loader,
            loaderVersion,
            javaMajor,
            query,
            provider,
            projectId);
    }
}

