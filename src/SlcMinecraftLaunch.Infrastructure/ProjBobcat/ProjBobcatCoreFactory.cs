using ProjBobcat.DefaultComponent.Launch;
using ProjBobcat.DefaultComponent.Launch.GameCore;
using ProjBobcat.DefaultComponent.Logging;
using ProjBobcat.Interface;
using SlcMinecraftLaunch.Infrastructure.Configuration;
using SlcMinecraftLaunch.Infrastructure.Persistence;

namespace SlcMinecraftLaunch.Infrastructure.ProjBobcat;

internal sealed record ProjBobcatCoreComponents(
    IGameCore GameCore,
    IVersionLocator VersionLocator,
    ILauncherAccountParser AccountParser,
    string RootPath);

internal static class ProjBobcatCoreFactory
{
    public static async Task<ProjBobcatCoreComponents> CreateAsync(
        PortableLauncherPaths paths,
        CancellationToken cancellationToken)
    {
        var clientToken = await new ClientTokenStore(paths.DataRoot)
            .LoadOrCreateAsync(cancellationToken)
            .ConfigureAwait(false);
        await new LegacyAccountDataSanitizer(paths.GameRoot)
            .SanitizeAsync(cancellationToken)
            .ConfigureAwait(false);

        // PROJBOBCAT: ILauncherAccountParser remains the account contract, but
        // the product is offline-only, so tokens are kept in memory and never saved.
        var accountParser = new InMemoryLauncherAccountParser(clientToken);
        var profileParser = new DefaultLauncherProfileParser(paths.GameRoot, clientToken);

        // PROJBOBCAT: DefaultVersionLocator(string, Guid) performs version scanning,
        // launcher profile parsing, and inheritsFrom chain resolution.
        var versionLocator = new DefaultVersionLocator(paths.GameRoot, clientToken)
        {
            LauncherAccountParser = accountParser,
            LauncherProfileParser = profileParser
        };

        // PROJBOBCAT: DefaultGameCore is the launch orchestrator and must own
        // authentication, argument construction, classpath assembly, and launching.
        var gameCore = new DefaultGameCore
        {
            ClientToken = clientToken,
            RootPath = paths.GameRoot,
            VersionLocator = versionLocator,
            GameLogResolver = new DefaultGameLogResolver()
        };

        return new ProjBobcatCoreComponents(
            gameCore,
            versionLocator,
            accountParser,
            paths.GameRoot);
    }
}
