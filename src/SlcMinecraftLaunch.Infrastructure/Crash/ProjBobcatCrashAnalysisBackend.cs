using ProjBobcat.DefaultComponent.LogAnalysis;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Infrastructure.Crash;

public sealed class ProjBobcatCrashAnalysisBackend : ICrashAnalysisBackend
{
    public async Task<IReadOnlyList<CrashInsight>> AnalyzeAsync(
        CrashAnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run<IReadOnlyList<CrashInsight>>(async () =>
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

            var insights = new List<CrashInsight>();
            foreach (var report in analyzer.GenerateReport())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var insight = CrashInsightClassifier.Classify(
                    report.Cause.ToString(),
                    report.From,
                    report.Details?.ToArray());
                insights.Add(await CrashModAttributor
                    .AttributeAsync(insight, request, cancellationToken)
                    .ConfigureAwait(false));
            }

            cancellationToken.ThrowIfCancellationRequested();
            return insights;
        }, cancellationToken).ConfigureAwait(false);
    }
}

