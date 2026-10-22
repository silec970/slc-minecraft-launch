using System.Text;
using System.Text.RegularExpressions;
using ProjBobcat.Class.Helper;
using SlcMinecraftLaunch.Domain.Models;
using SlcMinecraftLaunch.Infrastructure.Mods;

namespace SlcMinecraftLaunch.Infrastructure.Crash;

public static class CrashModAttributor
{
    private static readonly string[] EvidenceMarkers =
    [
        "at ",
        "Caused by",
        "Exception",
        "Mixin",
        "Caught exception from",
        "Failed to create mod instance",
        "-- MOD",
        "Failure message"
    ];

    public static async Task<CrashInsight> AttributeAsync(
        CrashInsight insight,
        CrashAnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        if (insight.Category != CrashCategory.Mod || !IsExistingFile(insight.Source))
        {
            return insight.Category == CrashCategory.Mod
                ? insight with { AttributionKey = "Crash.ModAttribution.Uncertain" }
                : insight;
        }

        var logText = await ReadBoundedTextAsync(insight.Source!, cancellationToken)
            .ConfigureAwait(false);
        var evidenceLines = logText
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Where(line => EvidenceMarkers.Any(marker =>
                line.Contains(marker, StringComparison.OrdinalIgnoreCase)))
            .ToArray();
        if (evidenceLines.Length == 0)
        {
            return insight with { AttributionKey = "Crash.ModAttribution.Uncertain" };
        }

        var affectedMods = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // PROJBOBCAT: GamePathHelper.GetGamePath is the same isolated instance
        // path used by DefaultVersionLocator for version-scoped mod storage.
        var modsDirectory = request.VersionIsolation
            ? Path.Combine(
                request.RootPath,
                GamePathHelper.GetGamePath(request.VersionId),
                "mods")
            : Path.Combine(request.RootPath, "mods");
        if (!Directory.Exists(modsDirectory))
        {
            return insight with { AttributionKey = "Crash.ModAttribution.Uncertain" };
        }

        foreach (var jarPath in Directory.EnumerateFiles(
                     modsDirectory,
                     "*.jar",
                     SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fileName = Path.GetFileName(jarPath);
            string? modId = null;
            try
            {
                modId = (await ModMetadataReader
                        .ReadAsync(jarPath, cancellationToken)
                        .ConfigureAwait(false))
                    .ModId;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // The filename remains valid evidence even if metadata is broken.
            }

            var matched = evidenceLines.Any(line =>
                line.Contains(fileName, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrWhiteSpace(modId) &&
                 Regex.IsMatch(
                     line,
                     $@"(?<![A-Za-z0-9_]){Regex.Escape(modId)}(?![A-Za-z0-9_])",
                     RegexOptions.IgnoreCase,
                     TimeSpan.FromMilliseconds(100))));
            if (matched)
            {
                affectedMods.Add(fileName);
            }
        }

        return affectedMods.Count switch
        {
            0 => insight with { AttributionKey = "Crash.ModAttribution.Uncertain" },
            1 => insight with
            {
                AffectedModFiles = affectedMods.ToArray(),
                AttributionKey = "Crash.ModAttribution.Single"
            },
            _ => insight with
            {
                AffectedModFiles = affectedMods.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
                AttributionKey = "Crash.ModAttribution.Multiple"
            }
        };
    }

    private static async Task<string> ReadBoundedTextAsync(
        string path,
        CancellationToken cancellationToken)
    {
        const int maxBytes = 16 * 1024 * 1024;
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var startOffset = Math.Max(0, stream.Length - maxBytes);
        stream.Seek(startOffset, SeekOrigin.Begin);
        var length = (int)Math.Min(stream.Length - startOffset, maxBytes);
        var buffer = new byte[length];
        var offset = 0;
        while (offset < length)
        {
            var read = await stream.ReadAsync(
                buffer.AsMemory(offset, length - offset),
                cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            offset += read;
        }

        return Encoding.UTF8.GetString(buffer, 0, offset);
    }

    private static bool IsExistingFile(string? path)
    {
        try
        {
            return !string.IsNullOrWhiteSpace(path) && File.Exists(path);
        }
        catch (Exception exception) when (exception is ArgumentException
                                               or NotSupportedException
                                               or PathTooLongException
                                               or IOException)
        {
            return false;
        }
    }
}
