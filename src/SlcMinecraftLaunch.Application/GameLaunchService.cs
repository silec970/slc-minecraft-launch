using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Application;

public sealed class GameLaunchService(
    IGameRuntimeProvider runtimeProvider,
    IJavaRuntimeService javaRuntimeService,
    IPlayerProfileService playerProfileService,
    IVersionPreferencesStore preferencesStore) : IGameLaunchService
{
    private readonly SemaphoreSlim _launchGate = new(1, 1);

    public async Task<GameLaunchOutcome> LaunchAsync(
        string versionId,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!await _launchGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return new GameLaunchOutcome(false, "Launch.AlreadyRunning");
        }

        try
        {
            var playerName = await playerProfileService
                .GetPlayerNameAsync(cancellationToken)
                .ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(playerName))
            {
                return new GameLaunchOutcome(false, "Launch.PlayerNameRequired");
            }

            var runtime = await runtimeProvider.GetAsync(cancellationToken).ConfigureAwait(false);
            var versions = await runtime.GetVersionsAsync(cancellationToken).ConfigureAwait(false);
            var version = versions.FirstOrDefault(item =>
                string.Equals(item.Id, versionId, StringComparison.OrdinalIgnoreCase));
            if (version is null)
            {
                return new GameLaunchOutcome(false, "Launch.VersionNotFound");
            }

            progress?.Report(new OperationProgress(
                SmokeTestStage.DetectingJava,
                "Launch.PreparingJava",
                0.05));
            var java = await javaRuntimeService.EnsureAsync(
                version.Id,
                version.LoaderKind,
                progress: progress,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (java.Selected is null)
            {
                return new GameLaunchOutcome(
                    false,
                    java.MessageKey,
                    java.ErrorDetail);
            }

            progress?.Report(new OperationProgress(
                SmokeTestStage.CompletingResources,
                "Launch.CheckingResources",
                0.2));
            var resources = await runtime.CompleteResourcesAsync(
                new ResourceCompletionRequest(version.Id, java.Selected.ExecutablePath),
                progress,
                cancellationToken).ConfigureAwait(false);
            if (!resources.Succeeded)
            {
                return new GameLaunchOutcome(
                    false,
                    resources.MessageKey,
                    resources.ErrorDetail);
            }

            var preferences = await preferencesStore
                .LoadAsync(cancellationToken)
                .ConfigureAwait(false);
            var isolation = (preferences.GetValueOrDefault(version.Id) ?? new VersionPreference())
                .IsIsolated;

            progress?.Report(new OperationProgress(
                SmokeTestStage.Launching,
                "Launch.Starting",
                0.9));
            return await runtime.LaunchOfflineAsync(
                new GameLaunchRequest(
                    version.Id,
                    playerName,
                    java.Selected.ExecutablePath,
                    isolation),
                progress,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _launchGate.Release();
        }
    }
}
