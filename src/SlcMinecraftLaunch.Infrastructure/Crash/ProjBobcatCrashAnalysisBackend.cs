using ProjBobcat.DefaultComponent.LogAnalysis;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Infrastructure.Crash;

public sealed class ProjBobcatCrashAnalysisBackend : ICrashAnalysisBackend
{
    public Task<IReadOnlyList<CrashInsight>> AnalyzeAsync(
        CrashAnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<CrashInsight>>(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();

            // PROJBOBCAT: DefaultLogAnalyzer performs latest.log, crash-report,
            // hs_err, and loader-rule analysis with isolation-aware paths.
            var analyzer = new DefaultLogAnalyzer
            {
                RootPath = request.RootPath,
                GameId = request.VersionId,
                VersionIsolation = request.VersionIsolation
            };

            var insights = analyzer.GenerateReport()
                .Select(report => CrashInsightClassifier.Classify(
                    report.Cause.ToString(),
                    report.From,
                    report.Details?.ToArray()))
                .ToArray();

            cancellationToken.ThrowIfCancellationRequested();
            return insights;
        }, cancellationToken);
    }
}

