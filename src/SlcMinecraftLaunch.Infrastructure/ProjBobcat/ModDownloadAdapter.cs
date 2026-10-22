using ProjBobcat.Class.Model;
using ProjBobcat.DefaultComponent;
using ProjBobcat.Interface;
using SlcMinecraftLaunch.Domain.Models;
using SlcMinecraftLaunch.Infrastructure.Mods;

namespace SlcMinecraftLaunch.Infrastructure.ProjBobcat;

internal sealed class ModDownloadAdapter(
    IVersionLocator versionLocator,
    IHttpClientFactory httpClientFactory,
    string rootPath)
{
    public async Task<ResourceCompletionOutcome> DownloadModAsync(
        ModDownloadRequest request,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (!IsPathWithin(request.ModsDirectory, request.FileName))
        {
            return new ResourceCompletionOutcome(
                false,
                "Mods.InvalidFileName");
        }

        var gameInfo = versionLocator.GetGame(request.VersionId);
        if (gameInfo is not VersionInfo concreteVersion)
        {
            return new ResourceCompletionOutcome(
                false,
                "Download.VersionNotResolved");
        }

        // PROJBOBCAT: ResolveGame supplies the isolated version model required
        // by DefaultResourceCompleter even when a custom Mod resolver is used.
        var resolvedVersion = versionLocator.ResolveGame(
            concreteVersion,
            NativeReplacementPolicy.Disabled,
            null);
        if (resolvedVersion is null)
        {
            return new ResourceCompletionOutcome(
                false,
                "Download.VersionNotResolved");
        }

        using var completer = new DefaultResourceCompleter
        {
            HttpClientFactory = httpClientFactory,
            DownloadThread = 8,
            DownloadParts = 8,
            MaxDegreeOfParallelism = 2,
            TimeoutPerFile = Timeout.InfiniteTimeSpan,
            ResolverTimeout = Timeout.InfiniteTimeSpan,
            TotalRetry = 4,
            CheckFile = !string.IsNullOrWhiteSpace(request.Sha1),
            ResourceInfoResolvers = [new ModDownloadResourceResolver(request)]
        };
        completer.DownloadFileChangedEvent += (_, eventArgs) =>
        {
            progress?.Report(new OperationProgress(
                SmokeTestStage.DownloadingMod,
                "Mods.Downloading",
                0.1 + (0.85 * OperationProgress.NormalizeFraction(
                    eventArgs.ProgressPercentage.NormalizedValue)),
                eventArgs.BytesReceived,
                eventArgs.TotalBytes,
                eventArgs.Speed));
        };

        var result = await completer.CheckAndDownloadTaskAsync(
            rootPath,
            true,
            resolvedVersion,
            cancellationToken).ConfigureAwait(false);
        if (result.TaskStatus is TaskResultStatus.Error or TaskResultStatus.PartialSuccess ||
            result.Value?.FailedFiles.Count > 0)
        {
            return new ResourceCompletionOutcome(
                false,
                "Mods.DownloadFailed",
                result.Message,
                result.Value?.FailedFiles.Count ?? 0);
        }

        progress?.Report(new OperationProgress(
            SmokeTestStage.Completed,
            "Mods.Downloaded",
            1));
        return new ResourceCompletionOutcome(true, "Mods.Downloaded");
    }

    private static bool IsPathWithin(string directory, string fileName)
    {
        var root = Path.GetFullPath(directory) + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(Path.Combine(directory, fileName));
        return candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }
}
