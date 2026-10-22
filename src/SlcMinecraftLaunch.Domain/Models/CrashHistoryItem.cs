namespace SlcMinecraftLaunch.Domain.Models;

public sealed record CrashHistoryItem(
    string ReportId,
    CrashAnalysisReport Report,
    string ReportPath,
    string? OriginalLogPath)
{
    public string DisplayTime => Report.AnalyzedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss");
    public bool HasIssues => Report.HasIssues;
    public string SummaryKey => HasIssues ? "Crash.History.Issues" : "Crash.History.NoIssues";
}
