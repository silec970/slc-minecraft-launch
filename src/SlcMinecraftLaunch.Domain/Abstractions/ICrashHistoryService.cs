using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Domain.Abstractions;

public interface ICrashHistoryService
{
    Task<IReadOnlyList<CrashHistoryItem>> GetHistoryAsync(
        string? versionId = null,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        string reportId,
        CancellationToken cancellationToken = default);

    Task<int> CleanupExpiredAsync(
        CancellationToken cancellationToken = default);
}
