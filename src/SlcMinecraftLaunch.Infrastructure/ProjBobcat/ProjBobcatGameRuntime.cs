using ProjBobcat.Class.Model;
using ProjBobcat.DefaultComponent.Launch;
using ProjBobcat.DefaultComponent.Launch.GameCore;
using ProjBobcat.DefaultComponent.Logging;
using ProjBobcat.Interface;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;
using SlcMinecraftLaunch.Infrastructure.Configuration;
using SlcMinecraftLaunch.Infrastructure.Persistence;

namespace SlcMinecraftLaunch.Infrastructure.ProjBobcat;

public sealed class ProjBobcatGameRuntime : IGameRuntime
{
    private readonly IGameCore _gameCore;
    private readonly IVersionLocator _versionLocator;

    private ProjBobcatGameRuntime(
        IGameCore gameCore,
        IVersionLocator versionLocator,
        string rootPath)
    {
        _gameCore = gameCore;
        _versionLocator = versionLocator;
        RootPath = rootPath;
    }

    public string RootPath { get; }

    public static async Task<ProjBobcatGameRuntime> CreateAsync(
        PortableLauncherPaths paths,
        CancellationToken cancellationToken = default)
    {
        var clientToken = await new ClientTokenStore(paths.DataRoot)
            .LoadOrCreateAsync(cancellationToken)
            .ConfigureAwait(false);

        // PROJBOBCAT: 2.1.2 uses DefaultLauncherAccountParser and
        // DefaultLauncherProfileParser to own launcher account/profile persistence.
        var accountParser = CreateAccountParser(paths.GameRoot, clientToken);
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

        return new ProjBobcatGameRuntime(gameCore, versionLocator, paths.GameRoot);
    }

    public Task<GameRuntimeSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // PROJBOBCAT: IVersionLocator.GetAllGames is the only supported source
        // for the installed version list.
        var versions = _versionLocator.GetAllGames()
            .Select(version => version switch
            {
                VersionInfo concreteVersion => concreteVersion.Id,
                _ => version.DirName
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Task.FromResult(new GameRuntimeSnapshot(RootPath, versions, true));
    }

    private static DefaultLauncherAccountParser CreateAccountParser(string rootPath, Guid clientToken)
    {
        // The 2.1.2 type marks a constructor-assigned property as required, but
        // its public constructor has no [SetsRequiredMembers]. Invoke that public
        // constructor so the package can load an existing account file without
        // clearing it through an object initializer.
        return Activator.CreateInstance(
                   typeof(DefaultLauncherAccountParser),
                   rootPath,
                   clientToken) as DefaultLauncherAccountParser
               ?? throw new InvalidOperationException("Unable to create the ProjBobcat account parser.");
    }

    public ValueTask DisposeAsync()
    {
        _gameCore.Dispose();
        return ValueTask.CompletedTask;
    }
}

