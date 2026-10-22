using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.LauncherProfile;
using ProjBobcat.Class.Model.YggdrasilAuth;
using ProjBobcat.DefaultComponent.Authenticator;
using ProjBobcat.DefaultComponent.Launch;
using ProjBobcat.Event;
using ProjBobcat.Interface;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Infrastructure.ProjBobcat;

internal sealed class LaunchAdapter(
    IGameCore gameCore,
    IVersionLocator versionLocator,
    ILauncherAccountParser accountParser,
    ResourceDownloadAdapter resourceDownloadAdapter,
    string rootPath)
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

        var completion = await resourceDownloadAdapter.CompleteResourcesAsync(
            new ResourceCompletionRequest(request.Version, request.JavaPath),
            null,
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
        // The desktop application remains offline-only by product decision.
        var offlineAuth = new OfflineAuthenticator
        {
            // PROJBOBCAT: OfflineAuthenticator and ILauncherAccountParser own
            // offline UUID and launcher_accounts.json handling.
            Username = request.OfflineUsername,
            LauncherAccountParser = accountParser
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
            GamePath = rootPath,
            GameResourcePath = rootPath,
            Version = request.Version,
            UseShellExecute = false,
            EnableXmlLoggingOutput = false,
            NativeReplacementPolicy = NativeReplacementPolicy.Disabled,
            WindowTitle = "SLC Smoke Test",
            VersionLocator = versionLocator,
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
        gameCore.GameExitEventDelegate += OnExit;
        gameCore.GameLogEventDelegate += OnGameLog;

        try
        {
            var launchResult = await gameCore
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
            gameCore.GameExitEventDelegate -= OnExit;
            gameCore.GameLogEventDelegate -= OnGameLog;
        }
    }

    public async Task<GameLaunchOutcome> LaunchOfflineAsync(
        GameLaunchRequest request,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var javaInfo = new JavaRuntimeInfo(
            request.JavaExecutablePath,
            OSPlatform.Windows,
            RuntimeInformation.ProcessArchitecture,
            false,
            false);

        // PROJBOBCAT: OfflineAuthenticator owns offline UUID generation and
        // derives identity solely from the configured player name.
        var offlineAuth = new OfflineAuthenticator
        {
            Username = request.PlayerName,
            LauncherAccountParser = accountParser
        };
        var authResult = await offlineAuth.AuthTaskAsync(false).ConfigureAwait(false);
        if (authResult.AuthStatus != AuthStatus.Succeeded)
        {
            return new GameLaunchOutcome(
                false,
                "Launch.AuthenticationFailed",
                authResult.Error?.ErrorMessage);
        }

        var instanceSettings = request.InstanceSettings ?? new InstanceLaunchSettings();
        var gameArguments = new GameArguments
        {
            Java = javaInfo,
            MinMemory = (uint)Math.Max(512, instanceSettings.MinMemoryMb),
            MaxMemory = (uint)Math.Max(instanceSettings.MinMemoryMb, instanceSettings.MaxMemoryMb),
            GcType = GcType.G1Gc,
            Resolution = new ResolutionModel
            {
                Width = (uint)Math.Max(320, instanceSettings.WindowWidth),
                Height = (uint)Math.Max(240, instanceSettings.WindowHeight),
                FullScreen = instanceSettings.Fullscreen
            },
            AdditionalJvmArguments = instanceSettings.JvmArguments ?? [],
            AdvanceArguments = string.Join(
                ' ',
                (instanceSettings.GameArguments ?? [])
                    .Select(QuoteLaunchArgument))
        };
        var launchSettings = new LaunchSettings
        {
            GameName = request.VersionId,
            GamePath = rootPath,
            GameResourcePath = rootPath,
            Version = request.VersionId,
            UseShellExecute = false,
            EnableXmlLoggingOutput = false,
            NativeReplacementPolicy = NativeReplacementPolicy.Disabled,
            WindowTitle = request.VersionId,
            VersionLocator = versionLocator,
            Authenticator = offlineAuth,
            VersionInsulation = request.VersionIsolation,
            LauncherName = "SlcMinecraftLaunch",
            FallBackGameArguments = gameArguments,
            GameArguments = gameArguments,
            GameEnvironmentVariables = [],
            UseV4NetworkingStack = false,
            PreferUtf8Encoding = true,
            AutoApplyFixForOldMultiPlayerGame = false,
            OldGameTrustStorePath = null
        };

        // PROJBOBCAT: DefaultGameCore performs authentication, argument
        // construction, classpath assembly, and process launch.
        var launchResult = await gameCore
            .LaunchTaskAsync(launchSettings)
            .ConfigureAwait(false);
        if (launchResult.ErrorType != LaunchErrorType.None ||
            launchResult.GameProcess is null)
        {
            return new GameLaunchOutcome(
                false,
                "Launch.StartFailed",
                launchResult.Error?.Exception?.ToString()
                ?? launchResult.Error?.Cause
                ?? launchResult.Error?.ErrorMessage
                ?? launchResult.Error?.Error);
        }

        progress?.Report(new OperationProgress(
            SmokeTestStage.Completed,
            "Launch.Started",
            1));
        return new GameLaunchOutcome(true, "Launch.Started");
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

    private static string QuoteLaunchArgument(string argument)
    {
        return argument.Any(char.IsWhiteSpace) && !argument.StartsWith('"')
            ? $"\"{argument.Replace("\"", "\\\"", StringComparison.Ordinal)}\""
            : argument;
    }
}
