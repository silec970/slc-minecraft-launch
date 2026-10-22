using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Downloading;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model.YggdrasilAuth;
using ProjBobcat.DefaultComponent;
using ProjBobcat.DefaultComponent.Authenticator;
using ProjBobcat.DefaultComponent.Launch;
using ProjBobcat.DefaultComponent.Launch.GameCore;
using ProjBobcat.DefaultComponent.Logging;
using ProjBobcat.DefaultComponent.ResourceInfoResolver;
using ProjBobcat.Event;
using ProjBobcat.Interface;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;
using SlcMinecraftLaunch.Infrastructure.Configuration;
using SlcMinecraftLaunch.Infrastructure.Persistence;

namespace SlcMinecraftLaunch.Infrastructure.ProjBobcat;

public sealed class ProjBobcatGameRuntime : IGameRuntime
{
    private static readonly string[] FatalLogMarkers =
    [
        "ClassNotFoundException",
        "UnsatisfiedLinkError",
        "NoClassDefFoundError",
        "UnsupportedClassVersionError",
        "Could not find or load main class",
        "Failed to launch"
    ];

    private readonly IGameCore _gameCore;
    private readonly IVersionLocator _versionLocator;
    private readonly ILauncherAccountParser _accountParser;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly DownloadSourceOptions _downloadSourceOptions;

    private ProjBobcatGameRuntime(
        IGameCore gameCore,
        IVersionLocator versionLocator,
        ILauncherAccountParser accountParser,
        IHttpClientFactory httpClientFactory,
        DownloadSourceOptions downloadSourceOptions,
        string rootPath)
    {
        _gameCore = gameCore;
        _versionLocator = versionLocator;
        _accountParser = accountParser;
        _httpClientFactory = httpClientFactory;
        _downloadSourceOptions = downloadSourceOptions;
        RootPath = rootPath;
    }

    public string RootPath { get; }

    internal IVersionLocator VersionLocator => _versionLocator;

    public static async Task<ProjBobcatGameRuntime> CreateAsync(
        PortableLauncherPaths paths,
        IHttpClientFactory httpClientFactory,
        DownloadSourceOptions downloadSourceOptions,
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

        return new ProjBobcatGameRuntime(
            gameCore,
            versionLocator,
            accountParser,
            httpClientFactory,
            downloadSourceOptions,
            paths.GameRoot);
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

    public Task<IReadOnlyList<GameVersionDescriptor>> GetVersionsAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // PROJBOBCAT: version discovery always starts from the locator's
        // GetAllGames result; no version directory JSON is read here.
        var versions = _versionLocator.GetAllGames()
            .Select(version => version switch
            {
                VersionInfo concrete => CreateVersionDescriptor(concrete),
                _ => new GameVersionDescriptor(
                    version.DirName,
                    string.IsNullOrWhiteSpace(version.Name) ? version.DirName : version.Name,
                    version.DirName,
                    LoaderKind.Other,
                    null,
                    null,
                    null,
                    true)
            })
            .OrderBy(version => version.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Task.FromResult<IReadOnlyList<GameVersionDescriptor>>(versions);
    }

    public async Task<ResourceCompletionOutcome> CompleteResourcesAsync(
        ResourceCompletionRequest request,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var gameInfo = _versionLocator.GetGame(request.VersionId);
        if (gameInfo is not VersionInfo concreteVersion)
        {
            return new ResourceCompletionOutcome(
                false,
                "Download.VersionNotResolved",
                "The ProjBobcat locator returned a broken version.");
        }

        var javaInfo = new JavaRuntimeInfo(
            request.JavaPath,
            OSPlatform.Windows,
            RuntimeInformation.ProcessArchitecture,
            false,
            false);

        // PROJBOBCAT: ResolveGame expands inheritsFrom and computes resources
        // using the locator-owned version metadata.
        var resolvedVersion = _versionLocator.ResolveGame(
            concreteVersion,
            NativeReplacementPolicy.Disabled,
            javaInfo);

        if (resolvedVersion is null)
        {
            return new ResourceCompletionOutcome(
                false,
                "Download.VersionNotResolved",
                "The ProjBobcat locator could not resolve the version.");
        }

        progress?.Report(new OperationProgress(
            SmokeTestStage.CompletingResources,
            "Progress.CompletingResources",
            0.25));

        using var completer = new DefaultResourceCompleter
        {
            // PROJBOBCAT: DefaultResourceCompleter is the sole multi-part
            // download, retry, resume, and hash validation implementation.
            HttpClientFactory = _httpClientFactory,
            DownloadThread = 16,
            DownloadParts = 16,
            MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount, 1, 8),
            TotalRetry = 4,
            CheckFile = true,
            ResourceInfoResolvers =
            [
                new AssetInfoResolver
                {
                    // PROJBOBCAT: AssetInfoResolver downloads and validates
                    // asset indexes and asset objects through its public API.
                    HttpClientFactory = _httpClientFactory,
                    AssetIndexUriRoots = ToUriRoots(_downloadSourceOptions.AssetIndexRoots),
                    AssetUriRoots = ToUriRoots(_downloadSourceOptions.AssetObjectRoots)
                },
                new LibraryInfoResolver
                {
                    LibraryUriRoots = ToUriRoots(_downloadSourceOptions.LibraryRoots),
                    ForgeUriRoots = ToUriRoots(_downloadSourceOptions.ForgeFileRoots),
                    ForgeMavenUriRoots = ToUriRoots(_downloadSourceOptions.ForgeMavenRoots),
                    ForgeMavenOldUriRoots = ToUriRoots(_downloadSourceOptions.ForgeMavenRoots),
                    FabricMavenUriRoots = ToUriRoots(_downloadSourceOptions.FabricMavenRoots),
                    QuiltMavenUriRoots = ToUriRoots(_downloadSourceOptions.QuiltMavenRoots)
                },
                new VersionInfoResolver
                {
                    VersionUriRoots = ToUriRoots(_downloadSourceOptions.VersionMetadataRoots)
                },
                new GameLoggingInfoResolver()
            ]
        };

        completer.GameResourceInfoResolveStatus += (_, eventArgs) =>
        {
            if (string.IsNullOrWhiteSpace(eventArgs.Status))
            {
                return;
            }

            progress?.Report(new OperationProgress(
                SmokeTestStage.CompletingResources,
                "Progress.CompletingResources",
                0.25 + (0.5 * OperationProgress.NormalizeFraction(eventArgs.Progress.NormalizedValue))));
        };

        completer.DownloadFileChangedEvent += (_, eventArgs) =>
        {
            progress?.Report(new OperationProgress(
                SmokeTestStage.CompletingResources,
                "Progress.DownloadingResources",
                0.25 + (0.5 * OperationProgress.NormalizeFraction(eventArgs.ProgressPercentage.NormalizedValue)),
                eventArgs.BytesReceived,
                eventArgs.TotalBytes,
                eventArgs.Speed));
        };

        var completionResult = await completer.CheckAndDownloadTaskAsync(
            RootPath,
            true,
            resolvedVersion,
            cancellationToken).ConfigureAwait(false);

        if (completionResult.TaskStatus is TaskResultStatus.Error or TaskResultStatus.PartialSuccess ||
            completionResult.Value?.FailedFiles.Count > 0)
        {
            var failedFiles = completionResult.Value?.FailedFiles.Count ?? 0;
            return new ResourceCompletionOutcome(
                false,
                "Download.ResourceCompletionFailed",
                $"{completionResult.Message} Failed files: {failedFiles}.",
                failedFiles);
        }

        progress?.Report(new OperationProgress(
            SmokeTestStage.Completed,
            "Download.Completed",
            1));
        return new ResourceCompletionOutcome(true, "Download.Completed");
    }

    public async Task<SmokeLaunchOutcome> LaunchSmokeAsync(
        SmokeLaunchRequest request,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var javaInfo = new JavaRuntimeInfo(
            request.JavaPath,
            OSPlatform.Windows,
            RuntimeInformation.ProcessArchitecture,
            false,
            false);

        var completion = await CompleteResourcesAsync(
            new ResourceCompletionRequest(request.Version, request.JavaPath),
            progress,
            cancellationToken).ConfigureAwait(false);
        if (!completion.Succeeded)
        {
            return new SmokeLaunchOutcome(
                false,
                completion.MessageKey,
                null,
                completion.ErrorDetail);
        }

        progress?.Report(new OperationProgress(
            SmokeTestStage.Authenticating,
            "Progress.OfflineAuthentication",
            0.77));

        // This authenticator is restricted to the explicit smoke-test path.
        // Production authentication must use the ProjBobcat Microsoft model.
        var offlineAuth = new OfflineAuthenticator
        {
            // PROJBOBCAT: OfflineAuthenticator and ILauncherAccountParser own
            // offline UUID and launcher_accounts.json handling.
            Username = request.OfflineUsername,
            LauncherAccountParser = _accountParser
        };
        var authResult = await offlineAuth.AuthTaskAsync(false).ConfigureAwait(false);
        if (authResult.AuthStatus != AuthStatus.Succeeded)
        {
            return new SmokeLaunchOutcome(
                false,
                "Smoke.AuthenticationFailed",
                null,
                authResult.Error?.ErrorMessage ?? "Offline authentication failed.");
        }

        var gameArguments = new GameArguments
        {
            Java = javaInfo,
            MinMemory = 1024,
            MaxMemory = 4096,
            GcType = GcType.G1Gc,
            Resolution = null
        };

        // PROJBOBCAT: LaunchSettings is the only supported configuration object
        // for memory, JVM/game arguments, isolation, and the selected Java.
        var launchSettings = new LaunchSettings
        {
            GameName = request.Version,
            GamePath = RootPath,
            GameResourcePath = RootPath,
            Version = request.Version,
            UseShellExecute = false,
            EnableXmlLoggingOutput = false,
            NativeReplacementPolicy = NativeReplacementPolicy.Disabled,
            WindowTitle = "SLC Smoke Test",
            VersionLocator = _versionLocator,
            Authenticator = offlineAuth,
            VersionInsulation = true,
            LauncherName = "SlcMinecraftLaunch",
            FallBackGameArguments = gameArguments,
            GameArguments = gameArguments,
            GameEnvironmentVariables = [],
            UseV4NetworkingStack = false,
            PreferUtf8Encoding = true,
            AutoApplyFixForOldMultiPlayerGame = false,
            OldGameTrustStorePath = null
        };

        progress?.Report(new OperationProgress(
            SmokeTestStage.Launching,
            "Progress.LaunchingGame",
            0.8));

        var exitCompletion = new TaskCompletionSource<GameExitEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var fatalLogs = new ConcurrentQueue<string>();

        void OnExit(object? sender, GameExitEventArgs eventArgs)
        {
            if (string.Equals(eventArgs.SourceGameId, request.Version, StringComparison.OrdinalIgnoreCase))
            {
                exitCompletion.TrySetResult(eventArgs);
            }
        }

        void OnGameLog(object? sender, GameLogEventArgs eventArgs)
        {
            var content = eventArgs.Content ?? eventArgs.RawContent;
            if (!string.IsNullOrWhiteSpace(content) &&
                FatalLogMarkers.Any(marker => content.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            {
                fatalLogs.Enqueue(content);
            }
        }

        // PROJBOBCAT: game logs and process exit state are consumed through the
        // core's public event contracts rather than by tailing stdout ourselves.
        _gameCore.GameExitEventDelegate += OnExit;
        _gameCore.GameLogEventDelegate += OnGameLog;

        try
        {
            var launchResult = await _gameCore
                .LaunchTaskAsync(launchSettings)
                .ConfigureAwait(false);

            if (launchResult.ErrorType != LaunchErrorType.None ||
                launchResult.GameProcess is null)
            {
                return new SmokeLaunchOutcome(
                    false,
                    "Smoke.LaunchFailed",
                    null,
                    launchResult.Error?.Exception?.ToString()
                    ?? launchResult.Error?.Cause
                    ?? launchResult.Error?.ErrorMessage
                    ?? launchResult.Error?.Error
                    ?? $"ProjBobcat launch failed with {launchResult.ErrorType}.");
            }

            progress?.Report(new OperationProgress(
                SmokeTestStage.VerifyingProcess,
                "Progress.VerifyingProcess",
                0.92));

            var healthCheckDuration = request.HealthCheckDuration <= TimeSpan.Zero
                ? TimeSpan.FromSeconds(5)
                : request.HealthCheckDuration;
            var exited = await Task.WhenAny(
                    exitCompletion.Task,
                    Task.Delay(healthCheckDuration, cancellationToken))
                .ConfigureAwait(false);

            if (exited == exitCompletion.Task)
            {
                var exit = await exitCompletion.Task.ConfigureAwait(false);
                return new SmokeLaunchOutcome(
                    false,
                    "Smoke.ProcessExitedEarly",
                    exit.ExitCode,
                    exit.Exception?.ToString());
            }

            if (fatalLogs.TryPeek(out var fatalLog))
            {
                return new SmokeLaunchOutcome(
                    false,
                    "Smoke.FatalStartupLog",
                    launchResult.GameProcess.HasExited ? launchResult.GameProcess.ExitCode : null,
                    fatalLog);
            }

            if (launchResult.GameProcess.HasExited)
            {
                return new SmokeLaunchOutcome(
                    false,
                    "Smoke.ProcessExitedEarly",
                    launchResult.GameProcess.ExitCode);
            }

            await StopProcessAsync(launchResult.GameProcess).ConfigureAwait(false);

            progress?.Report(new OperationProgress(
                SmokeTestStage.Completed,
                "Smoke.Succeeded",
                1));

            return new SmokeLaunchOutcome(
                true,
                "Smoke.Succeeded",
                null,
                Warnings: Array.Empty<string>());
        }
        finally
        {
            _gameCore.GameExitEventDelegate -= OnExit;
            _gameCore.GameLogEventDelegate -= OnGameLog;
        }
    }

    private static async Task StopProcessAsync(Process process)
    {
        if (process.HasExited)
        {
            return;
        }

        process.Kill(entireProcessTree: true);
        await process.WaitForExitAsync().ConfigureAwait(false);
    }

    private static IReadOnlyList<DownloadUriInfo> ToUriRoots(IReadOnlyList<string> roots)
    {
        return roots
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Select((root, index) => new DownloadUriInfo(root, index + 1))
            .ToArray();
    }

    private static GameVersionDescriptor CreateVersionDescriptor(VersionInfo version)
    {
        var rawVersion = version.RawVersion;
        // PROJBOBCAT: GameVersionHelper classifies the expanded raw version
        // metadata so loader detection follows the core's resolver logic.
        var loaderKind = rawVersion is null
            ? LoaderKind.Vanilla
            : GameVersionHelper.TryGetGameModLoaderType(rawVersion) switch
            {
                ModLoaderType.NeoForge => LoaderKind.NeoForge,
                ModLoaderType.Forge => LoaderKind.Forge,
                ModLoaderType.Fabric => LoaderKind.Fabric,
                ModLoaderType.Quilt => LoaderKind.Quilt,
                ModLoaderType.Optifine => LoaderKind.OptiFine,
                _ => DetectFallbackLoader(version.Id)
            };

        var loaderVersion = loaderKind switch
        {
            LoaderKind.Forge when rawVersion is not null
                => GameVersionHelper.TryGetForgeVersion(rawVersion),
            LoaderKind.NeoForge when rawVersion is not null
                => GameVersionHelper.TryGetNeoForgeVersion(rawVersion),
            LoaderKind.Fabric when rawVersion is not null
                => GameVersionHelper.TryGetFabricVersion(rawVersion),
            _ => null
        };

        return new GameVersionDescriptor(
            version.Id,
            string.IsNullOrWhiteSpace(version.Name) ? version.Id : version.Name,
            version.GameBaseVersion,
            loaderKind,
            loaderVersion,
            version.JavaVersion?.MajorVersion,
            version.JavaVersion?.Component,
            false);
    }

    private static LoaderKind DetectFallbackLoader(string versionId)
    {
        if (versionId.Contains("neoforge", StringComparison.OrdinalIgnoreCase))
        {
            return LoaderKind.NeoForge;
        }

        if (versionId.Contains("forge", StringComparison.OrdinalIgnoreCase))
        {
            return LoaderKind.Forge;
        }

        if (versionId.Contains("fabric", StringComparison.OrdinalIgnoreCase))
        {
            return LoaderKind.Fabric;
        }

        if (versionId.Contains("quilt", StringComparison.OrdinalIgnoreCase))
        {
            return LoaderKind.Quilt;
        }

        if (versionId.Contains("optifine", StringComparison.OrdinalIgnoreCase))
        {
            return LoaderKind.OptiFine;
        }

        if (versionId.Contains("liteloader", StringComparison.OrdinalIgnoreCase))
        {
            return LoaderKind.LiteLoader;
        }

        return LoaderKind.Vanilla;
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
