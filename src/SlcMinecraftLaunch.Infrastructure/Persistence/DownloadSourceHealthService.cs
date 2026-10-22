using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Infrastructure.Persistence;

public sealed class DownloadSourceHealthService(
    JsonSourceHealthStore store) : ISourceHealthService
{
    private readonly SemaphoreSlim _updateLock = new(1, 1);

    public async Task<IReadOnlyDictionary<string, DownloadSourceScore>> GetScoresAsync(
        IReadOnlyList<string> sourceRoots,
        CancellationToken cancellationToken = default)
    {
        var stored = await store.LoadAsync(cancellationToken).ConfigureAwait(false);
        var result = new Dictionary<string, DownloadSourceScore>(StringComparer.OrdinalIgnoreCase);
        foreach (var sourceRoot in sourceRoots.Where(root => !string.IsNullOrWhiteSpace(root)))
        {
            result[sourceRoot] = stored.GetValueOrDefault(NormalizeSourceRoot(sourceRoot))
                                 ?? NewScore(sourceRoot);
        }

        return result;
    }

    public async Task RecordAsync(
        IReadOnlyList<DownloadSourceObservation> observations,
        CancellationToken cancellationToken = default)
    {
        if (observations.Count == 0)
        {
            return;
        }

        await _updateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var scores = new Dictionary<string, DownloadSourceScore>(
                await store.LoadAsync(cancellationToken).ConfigureAwait(false),
                StringComparer.OrdinalIgnoreCase);
            foreach (var observation in observations
                         .Where(item => !string.IsNullOrWhiteSpace(item.SourceRoot))
                         .GroupBy(item => NormalizeSourceRoot(item.SourceRoot), StringComparer.OrdinalIgnoreCase))
            {
                var current = scores.GetValueOrDefault(observation.Key)
                              ?? NewScore(observation.Key);
                var successes = observation.Sum(item => item.SuccessCount);
                var failures = observation.Sum(item => item.FailureCount);
                var consecutiveSuccesses = successes > 0
                    ? current.ConsecutiveSuccesses + successes
                    : failures > 0
                        ? 0
                        : current.ConsecutiveSuccesses;
                var score = current.Score;
                if (successes > 0)
                {
                    score += Math.Min(10, 1 + (int)Math.Log2(successes));
                }

                if (failures > 0)
                {
                    score -= Math.Min(30, failures * 5);
                }

                scores[observation.Key] = current with
                {
                    Score = Math.Clamp(score, 0, 100),
                    SuccessCount = current.SuccessCount + successes,
                    FailureCount = current.FailureCount + failures,
                    ConsecutiveSuccesses = consecutiveSuccesses,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
            }

            await store.SaveAsync(scores.Values.ToArray(), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _updateLock.Release();
        }
    }

    private static DownloadSourceScore NewScore(string sourceRoot)
    {
        return new DownloadSourceScore(
            NormalizeSourceRoot(sourceRoot),
            50,
            0,
            0,
            0,
            DateTimeOffset.UtcNow);
    }

    private static string NormalizeSourceRoot(string sourceRoot)
    {
        if (!Uri.TryCreate(sourceRoot, UriKind.Absolute, out var uri))
        {
            return sourceRoot.Trim();
        }

        var builder = new UriBuilder(uri)
        {
            Query = string.Empty,
            Fragment = string.Empty
        };
        if (!builder.Path.EndsWith('/'))
        {
            builder.Path += "/";
        }

        return builder.Uri.AbsoluteUri;
    }
}
