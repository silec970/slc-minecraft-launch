using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Domain.Abstractions;

public interface ICrashReportStore
{
    Task<string> SaveAsync(
        CrashAnalysisReport report,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CrashHistoryItem>> LoadHistoryAsync(
        string? versionId = null,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        string reportId,
        CancellationToken cancellationToken = default);

    Task<int> CleanupExpiredAsync(
        DateTimeOffset cutoff,
        CancellationToken cancellationToken = default);
}
