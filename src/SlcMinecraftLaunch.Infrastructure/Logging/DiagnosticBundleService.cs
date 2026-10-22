using System.IO.Compression;
using System.Text;
using System.Text.Json;
using SlcMinecraftLaunch.Domain.Abstractions;

namespace SlcMinecraftLaunch.Infrastructure.Logging;

public sealed class DiagnosticBundleService(
    string gameRoot,
    string dataRoot,
    IJavaDiscovery javaDiscovery) : IDiagnosticBundleService
{
    public async Task<string> CreateAsync(
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        var destination = Path.GetFullPath(destinationPath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var temporaryPath = $"{destination}.{Guid.NewGuid():N}.tmp";
        var javaRuntimes = await javaDiscovery.DiscoverAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using (var archive = ZipFile.Open(temporaryPath, ZipArchiveMode.Create))
            {
                AddRecentLogs(archive, cancellationToken);
                AddInstanceConfigurations(archive, cancellationToken);
                WriteEntry(
                    archive,
                    "java-runtimes.json",
                    JsonSerializer.Serialize(javaRuntimes, new JsonSerializerOptions { WriteIndented = true }));
                WriteEntry(
                    archive,
                    "diagnostic-manifest.json",
                    JsonSerializer.Serialize(new
                    {
                        createdAt = DateTimeOffset.UtcNow,
                        launcher = "SlcMinecraftLaunch",
                        containsCredentials = false,
                        gameRoot,
                        dataRoot
                    }, new JsonSerializerOptions { WriteIndented = true }));
            }

            File.Move(temporaryPath, destination, true);
            return destination;
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private void AddRecentLogs(ZipArchive archive, CancellationToken cancellationToken)
    {
        var logDirectory = Path.Combine(dataRoot, "logs");
        if (!Directory.Exists(logDirectory))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(logDirectory, "*.log", SearchOption.TopDirectoryOnly)
                     .OrderByDescending(File.GetLastWriteTimeUtc)
                     .Take(7))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var content = LogRedactor.Redact(File.ReadAllText(path));
            WriteEntry(archive, $"logs/{Path.GetFileName(path)}", content);
        }
    }

    private void AddInstanceConfigurations(
        ZipArchive archive,
        CancellationToken cancellationToken)
    {
        var versionsRoot = Path.Combine(gameRoot, "versions");
        if (!Directory.Exists(versionsRoot))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(
                     versionsRoot,
                     "instance.json",
                     SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var instanceId = Path.GetFileName(Path.GetDirectoryName(path)) ?? "instance";
            var content = LogRedactor.Redact(File.ReadAllText(path));
            WriteEntry(archive, $"instances/{instanceId}/instance.json", content);
        }
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var output = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        output.Write(content);
    }
}
