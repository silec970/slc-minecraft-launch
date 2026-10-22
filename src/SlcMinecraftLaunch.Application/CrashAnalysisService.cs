using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Application;

public sealed class CrashAnalysisService(
    IGameRuntimeProvider runtimeProvider,
    IVersionPreferencesStore preferencesStore,
    ICrashAnalysisBackend crashAnalysisBackend,
    ICrashReportStore crashReportStore) : ICrashAnalysisService
{
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
        var report = new CrashAnalysisReport(
            versionId,
            DateTimeOffset.Now,
            exitCode,
            insights);

        await crashReportStore.SaveAsync(report, cancellationToken).ConfigureAwait(false);
        return report;
    }
}

