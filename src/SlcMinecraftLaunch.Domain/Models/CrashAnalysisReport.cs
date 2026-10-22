namespace SlcMinecraftLaunch.Domain.Models;

public sealed record CrashAnalysisReport(
    string VersionId,
    DateTimeOffset AnalyzedAt,
    int? ExitCode,
    IReadOnlyList<CrashInsight> Insights)
{
    public bool HasIssues => Insights.Count > 0;
}

