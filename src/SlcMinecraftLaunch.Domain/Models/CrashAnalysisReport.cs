namespace SlcMinecraftLaunch.Domain.Models;

public sealed record CrashAnalysisReport(
    string VersionId,
    DateTimeOffset AnalyzedAt,
    int? ExitCode,
    IReadOnlyList<CrashInsight> Insights,
    string? ReportId = null,
    string? OriginalLogPath = null)
{
    public bool HasIssues => Insights.Count > 0;
}

