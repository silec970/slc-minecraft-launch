using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Application;

public sealed class GameLaunchService(
    IGameRuntimeProvider runtimeProvider,
    IJavaRuntimeService javaRuntimeService,
    IPlayerProfileService playerProfileService,
    IVersionPreferencesStore preferencesStore,
    IInstanceService instanceService) : IGameLaunchService
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

            var instance = await instanceService
                .GetManifestAsync(version.Id, cancellationToken)
                .ConfigureAwait(false);
            if (instance?.IsPlaceholder == true)
            {
                return new GameLaunchOutcome(false, "Launch.InstancePlaceholder");
            }

            if (instance is not null &&
                (!string.Equals(
                    instance.MinecraftVersion,
                    version.BaseVersion,
                    StringComparison.OrdinalIgnoreCase) ||
                 instance.LoaderKind != version.LoaderKind ||
                 !string.Equals(
                     instance.LoaderVersion,
                     version.LoaderVersion,
                     StringComparison.OrdinalIgnoreCase)))
            {
                return new GameLaunchOutcome(false, "Launch.InstanceMismatch");
            }

            var instanceSettings = instance?.LaunchSettings ?? new InstanceLaunchSettings();
            progress?.Report(new OperationProgress(
                SmokeTestStage.DetectingJava,
                "Launch.PreparingJava",
                0.05));
            var java = await javaRuntimeService.EnsureAsync(
                version.Id,
                version.LoaderKind,
                explicitJavaPath: instanceSettings.JavaExecutablePath,
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
                "Launch.CheckingFileIntegrity",
                0.2));
            var integrityObserver = new IntegrityObserver();
            var resources = await runtime.CompleteResourcesObservedAsync(
                new ResourceCompletionRequest(version.Id, java.Selected.ExecutablePath),
                integrityObserver,
                progress,
                cancellationToken).ConfigureAwait(false);
            if (!resources.Succeeded)
            {
                return new GameLaunchOutcome(
                    false,
                    "Launch.IntegrityCheckFailed",
                    CombineDetails(resources.MessageKey, resources.ErrorDetail));
            }

            var repairedFiles = integrityObserver.GetRepairedFiles();
            if (repairedFiles.Count > 0)
            {
                return new GameLaunchOutcome(
                    false,
                    "Launch.IntegrityRepaired",
                    string.Join(Environment.NewLine, repairedFiles));
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
                    isolation,
                    instanceSettings),
                progress,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _launchGate.Release();
        }
    }

    private static string CombineDetails(string messageKey, string? errorDetail)
    {
        return string.IsNullOrWhiteSpace(errorDetail)
            ? messageKey
            : $"{messageKey}{Environment.NewLine}{errorDetail}";
    }

    private sealed class IntegrityObserver : IResourceDownloadObserver
    {
        private readonly HashSet<string> _repairedFiles =
            new(StringComparer.OrdinalIgnoreCase);

        public void OnFileCompleted(ResourceDownloadFileResult result)
        {
            if (!result.Succeeded)
            {
                return;
            }

            lock (_repairedFiles)
            {
                _repairedFiles.Add(result.TargetPath);
            }
        }

        public IReadOnlyList<string> GetRepairedFiles()
        {
            lock (_repairedFiles)
            {
                return _repairedFiles
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
        }
    }
}
