using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Domain.Abstractions;

public interface ISourceHealthService
{
    Task<IReadOnlyDictionary<string, DownloadSourceScore>> GetScoresAsync(
        IReadOnlyList<string> sourceRoots,
        CancellationToken cancellationToken = default);

    Task RecordAsync(
        IReadOnlyList<DownloadSourceObservation> observations,
        CancellationToken cancellationToken = default);
}
