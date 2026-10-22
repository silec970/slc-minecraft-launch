using System.Text.Json;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Infrastructure.Persistence;

public sealed class JsonCrashReportStore(string dataRoot) : ICrashReportStore
{
    private readonly string _directory = Path.Combine(dataRoot, "crash-insights");
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public async Task<string> SaveAsync(
        CrashAnalysisReport report,
        CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_directory);
            var reportId = string.IsNullOrWhiteSpace(report.ReportId)
                ? Guid.NewGuid().ToString("N")
                : SanitizeId(report.ReportId);
            var storedReport = report with { ReportId = reportId };
            var safeVersion = Sanitize(report.VersionId);
            var reportPath = Path.Combine(_directory, $"{reportId}-{safeVersion}.json");
            var latestPath = Path.Combine(_directory, $"latest-{safeVersion}.json");

            await WriteAsync(reportPath, storedReport, cancellationToken).ConfigureAwait(false);
            await WriteAsync(latestPath, storedReport, cancellationToken).ConfigureAwait(false);
            return reportPath;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<IReadOnlyList<CrashHistoryItem>> LoadHistoryAsync(
        string? versionId = null,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_directory))
        {
            return [];
        }

        var items = new List<CrashHistoryItem>();
        foreach (var path in Directory.EnumerateFiles(_directory, "*.json", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Path.GetFileName(path).StartsWith("latest-", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var report = await ReadAsync(path, cancellationToken).ConfigureAwait(false);
            if (report is null ||
                (!string.IsNullOrWhiteSpace(versionId) &&
                 !string.Equals(report.VersionId, versionId, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var reportId = string.IsNullOrWhiteSpace(report.ReportId)
                ? Path.GetFileNameWithoutExtension(path)
                : report.ReportId;
            items.Add(new CrashHistoryItem(
                reportId,
                report,
                path,
                report.OriginalLogPath));
        }

        return items
            .OrderByDescending(item => item.Report.AnalyzedAt)
            .ToArray();
    }

    public async Task<bool> DeleteAsync(
        string reportId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reportId) || !Directory.Exists(_directory))
        {
            return false;
        }

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var matching = Directory
                .EnumerateFiles(_directory, "*.json", SearchOption.TopDirectoryOnly)
                .Where(path =>
                {
                    var name = Path.GetFileNameWithoutExtension(path);
                    return string.Equals(name, reportId, StringComparison.OrdinalIgnoreCase) ||
                           name.StartsWith($"{reportId}-", StringComparison.OrdinalIgnoreCase);
                })
                .ToArray();
            foreach (var path in matching)
            {
                cancellationToken.ThrowIfCancellationRequested();
                File.Delete(path);
            }

            return matching.Length > 0;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<int> CleanupExpiredAsync(
        DateTimeOffset cutoff,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_directory))
        {
            return 0;
        }

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var deleted = 0;
            foreach (var path in Directory.EnumerateFiles(_directory, "*.json", SearchOption.TopDirectoryOnly))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var report = await ReadAsync(path, cancellationToken).ConfigureAwait(false);
                if (report is not null && report.AnalyzedAt < cutoff)
                {
                    File.Delete(path);
                    deleted++;
                }
            }

            return deleted;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private static async Task<CrashAnalysisReport?> ReadAsync(
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var input = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<CrashAnalysisReport>(
                input,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static async Task WriteAsync(
        string path,
        CrashAnalysisReport report,
        CancellationToken cancellationToken)
    {
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";

        try
        {
            {
                await using var output = File.Create(temporaryPath);
                await JsonSerializer.SerializeAsync(
                    output,
                    report,
                    new JsonSerializerOptions { WriteIndented = true },
                    cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, path, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static string SanitizeId(string value)
    {
        var normalized = new string(value
            .Where(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_')
            .ToArray());
        return normalized.Length == 0 ? Guid.NewGuid().ToString("N") : normalized;
    }

    private static string Sanitize(string value)
    {
        return new string(value.Select(character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.'
                ? character
                : '_').ToArray());
    }
}
