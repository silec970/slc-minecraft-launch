using System.Text.Json;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Infrastructure.Persistence;

public sealed class JsonCrashReportStore(string dataRoot) : ICrashReportStore
{
    public async Task<string> SaveAsync(
        CrashAnalysisReport report,
        CancellationToken cancellationToken = default)
    {
        var directory = Path.Combine(dataRoot, "crash-insights");
        Directory.CreateDirectory(directory);
        var timestamp = report.AnalyzedAt.ToString("yyyyMMdd-HHmmss");
        var safeVersion = Sanitize(report.VersionId);
        var reportPath = Path.Combine(directory, $"{timestamp}-{safeVersion}.json");
        var latestPath = Path.Combine(directory, $"latest-{safeVersion}.json");

        await WriteAsync(reportPath, report, cancellationToken).ConfigureAwait(false);
        await WriteAsync(latestPath, report, cancellationToken).ConfigureAwait(false);
        return reportPath;
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

    private static string Sanitize(string value)
    {
        return string.Concat(value.Select(character =>
            Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
    }
}
