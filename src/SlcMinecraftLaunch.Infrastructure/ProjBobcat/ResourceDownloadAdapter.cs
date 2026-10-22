using System.Runtime.InteropServices;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Downloading;
using ProjBobcat.DefaultComponent;
using ProjBobcat.DefaultComponent.ResourceInfoResolver;
using ProjBobcat.Interface;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Infrastructure.ProjBobcat;

internal sealed class ResourceDownloadAdapter(
    IVersionLocator versionLocator,
    IHttpClientFactory httpClientFactory,
    DownloadSourceOptions downloadSourceOptions,
    ISourceHealthService sourceHealthService,
    string rootPath)
{
    public async Task<ResourceCompletionOutcome> CompleteResourcesAsync(
        ResourceCompletionRequest request,
        IResourceDownloadObserver? observer,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        var gameInfo = versionLocator.GetGame(request.VersionId);
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
        var resolvedVersion = versionLocator.ResolveGame(
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

        // PROJBOBCAT: DownloadUriInfo.Weight is the public weighted fallback
        // contract used to favor healthy roots without removing any source.
        var allSourceRoots = downloadSourceOptions.VersionMetadataRoots
            .Concat(downloadSourceOptions.AssetIndexRoots)
            .Concat(downloadSourceOptions.AssetObjectRoots)
            .Concat(downloadSourceOptions.LibraryRoots)
            .Concat(downloadSourceOptions.ForgeFileRoots)
            .Concat(downloadSourceOptions.ForgeMavenRoots)
            .Concat(downloadSourceOptions.FabricMavenRoots)
            .Concat(downloadSourceOptions.QuiltMavenRoots)
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var sourceScores = await sourceHealthService
            .GetScoresAsync(allSourceRoots, cancellationToken)
            .ConfigureAwait(false);

        using var completer = new DefaultResourceCompleter
        {
            // PROJBOBCAT: DefaultResourceCompleter is the sole multi-part
            // download, retry, resume, and hash validation implementation.
            HttpClientFactory = httpClientFactory,
            DownloadThread = 16,
            DownloadParts = 16,
            MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount, 1, 8),
            TimeoutPerFile = Timeout.InfiniteTimeSpan,
            ResolverTimeout = Timeout.InfiniteTimeSpan,
            TotalRetry = 4,
            CheckFile = true,
            ResourceInfoResolvers =
            [
                new AssetInfoResolver
                {
                    // PROJBOBCAT: AssetInfoResolver downloads and validates
                    // asset indexes and asset objects through its public API.
                    HttpClientFactory = httpClientFactory,
                    AssetIndexUriRoots = ToUriRoots(downloadSourceOptions.AssetIndexRoots, sourceScores),
                    AssetUriRoots = ToUriRoots(downloadSourceOptions.AssetObjectRoots, sourceScores)
                },
                new LibraryInfoResolver
                {
                    LibraryUriRoots = ToUriRoots(downloadSourceOptions.LibraryRoots, sourceScores),
                    ForgeUriRoots = ToUriRoots(downloadSourceOptions.ForgeFileRoots, sourceScores),
                    ForgeMavenUriRoots = ToUriRoots(downloadSourceOptions.ForgeMavenRoots, sourceScores),
                    ForgeMavenOldUriRoots = ToUriRoots(downloadSourceOptions.ForgeMavenRoots, sourceScores),
                    FabricMavenUriRoots = ToUriRoots(downloadSourceOptions.FabricMavenRoots, sourceScores),
                    QuiltMavenUriRoots = ToUriRoots(downloadSourceOptions.QuiltMavenRoots, sourceScores)
                },
                new VersionInfoResolver
                {
                    VersionUriRoots = ToUriRoots(downloadSourceOptions.VersionMetadataRoots, sourceScores)
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

        // PROJBOBCAT: DownloadFileCompletedEvent exposes the final per-file
        // source, checksum, and success state for task persistence and health.
        completer.DownloadFileCompletedEvent += (sender, eventArgs) =>
        {
            if (sender is not MultiSourceDownloadFile file)
            {
                return;
            }

            var sourceUrl = file.GetDownloadUrl();
            var targetPath = Path.IsPathRooted(file.FileName)
                ? file.FileName
                : Path.Combine(file.DownloadPath, file.FileName);
            observer?.OnFileCompleted(new ResourceDownloadFileResult(
                file.FileName,
                targetPath,
                FindSourceRoot(file.DownloadUris, sourceUrl),
                sourceUrl,
                file.FileSize,
                file.CheckSum,
                eventArgs.DownloadEventArgs.Success,
                eventArgs.DownloadEventArgs.Error?.Message));
        };

        var completionResult = await completer.CheckAndDownloadTaskAsync(
            rootPath,
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

    private static IReadOnlyList<DownloadUriInfo> ToUriRoots(
        IReadOnlyList<string> roots,
        IReadOnlyDictionary<string, DownloadSourceScore> scores)
    {
        return roots
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Select((root, index) =>
            {
                var priority = roots.Count - index;
                var score = scores.GetValueOrDefault(root)?.Score ?? 50;
                var healthWeight = Math.Clamp((int)Math.Round(score / 10d), 1, 10);
                return new DownloadUriInfo(root, Math.Max(1, priority * healthWeight));
            })
            .ToArray();
    }

    private static string? FindSourceRoot(
        IReadOnlyList<DownloadUriInfo> sources,
        string sourceUrl)
    {
        var matchingRoot = sources
            .Select(source => source.DownloadUri)
            .Where(root => sourceUrl.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(root => root.Length)
            .FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(matchingRoot))
        {
            return matchingRoot;
        }

        return Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri)
            ? uri.GetLeftPart(UriPartial.Authority) + "/"
            : null;
    }
}
