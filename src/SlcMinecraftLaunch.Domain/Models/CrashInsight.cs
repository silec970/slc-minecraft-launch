namespace SlcMinecraftLaunch.Domain.Models;

public sealed record CrashInsight(
    string CauseCode,
    CrashCategory Category,
    CrashSeverity Severity,
    string MessageKey,
    string SuggestionKey,
    string? Source = null,
    IReadOnlyList<string>? Details = null,
    IReadOnlyList<string>? AffectedModFiles = null,
    string? AttributionKey = null);

