using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Application;

public sealed class CrashAnalysisService(
    IGameRuntimeProvider runtimeProvider,
    IVersionPreferencesStore preferencesStore,
    ICrashAnalysisBackend crashAnalysisBackend,
    ICrashReportStore crashReportStore,
    IAppLogger appLogger) : ICrashAnalysisService, ICrashHistoryService
{
    private static readonly TimeSpan Retention = TimeSpan.FromDays(30);

    public async Task<CrashAnalysisReport> AnalyzeAsync(
        string versionId,
        int? exitCode = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(versionId);

        var runtime = await runtimeProvider.GetAsync(cancellationToken).ConfigureAwait(false);
        var preferences = await preferencesStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        var preference = preferences.GetValueOrDefault(versionId) ?? new VersionPreference();

        var request = new CrashAnalysisRequest(
            runtime.RootPath,
            versionId,
            preference.IsIsolated,
            exitCode);
        var insights = await crashAnalysisBackend
            .AnalyzeAsync(request, cancellationToken)
            .ConfigureAwait(false);
        var originalLogPath = insights
            .Select(insight => insight.Source)
            .Where(IsExistingFile)
            .OrderByDescending(path => path!.Contains(
                "crash-",
                StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();
        var report = new CrashAnalysisReport(
            versionId,
            DateTimeOffset.Now,
            exitCode,
            insights,
            Guid.NewGuid().ToString("N"),
            originalLogPath);

        await crashReportStore.SaveAsync(report, cancellationToken).ConfigureAwait(false);
        await CleanupExpiredCoreAsync(cancellationToken).ConfigureAwait(false);
        return report;
    }

    public async Task<IReadOnlyList<CrashHistoryItem>> GetHistoryAsync(
        string? versionId = null,
        CancellationToken cancellationToken = default)
    {
        await CleanupExpiredCoreAsync(cancellationToken).ConfigureAwait(false);
        return await crashReportStore
            .LoadHistoryAsync(versionId, cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<bool> DeleteAsync(
        string reportId,
        CancellationToken cancellationToken = default)
    {
        return crashReportStore.DeleteAsync(reportId, cancellationToken);
    }

    public Task<int> CleanupExpiredAsync(CancellationToken cancellationToken = default)
    {
        return crashReportStore.CleanupExpiredAsync(
            DateTimeOffset.Now - Retention,
            cancellationToken);
    }

    private async Task CleanupExpiredCoreAsync(CancellationToken cancellationToken)
    {
        try
        {
            await CleanupExpiredAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            appLogger.Warning($"Crash history cleanup failed: {exception.Message}");
        }
    }

    private static bool IsExistingFile(string? path)
    {
        try
        {
            return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
        }
        catch (Exception exception) when (exception is ArgumentException
                                               or NotSupportedException
                                               or PathTooLongException
                                               or IOException)
        {
            return false;
        }
    }
}

