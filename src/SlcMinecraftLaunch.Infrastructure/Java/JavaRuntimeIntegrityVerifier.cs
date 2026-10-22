using System.Security.Cryptography;
using System.Text.Json;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Infrastructure.Java;

public sealed record JavaIntegrityVerification(
    JavaIntegrityStatus Status,
    string? Detail = null);

public sealed class JavaRuntimeIntegrityVerifier
{
    public async Task<JavaIntegrityVerification> VerifyAsync(
        string runtimeDirectory,
        CancellationToken cancellationToken = default)
    {
        var markerPath = Path.Combine(runtimeDirectory, ".slc-ready");
        var manifestPath = Path.Combine(runtimeDirectory, ".integrity.json");
        if (!File.Exists(markerPath) || !File.Exists(manifestPath))
        {
            return new JavaIntegrityVerification(
                JavaIntegrityStatus.Unknown,
                "The managed runtime has no verification markers.");
        }

        try
        {
            await using var input = File.OpenRead(manifestPath);
            using var document = await JsonDocument.ParseAsync(
                input,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            var root = document.RootElement;
            if (!root.TryGetProperty("files", out var files) ||
                files.ValueKind != JsonValueKind.Object)
            {
                return new JavaIntegrityVerification(
                    JavaIntegrityStatus.Unknown,
                    "The integrity manifest has no local SHA256 records.");
            }

            var entries = files.EnumerateObject().ToArray();
            var source = root.TryGetProperty("source", out var sourceElement)
                ? sourceElement.GetString()
                : null;
            var isLegacyAdoptiumArchiveRecord =
                string.Equals(source, "adoptium", StringComparison.OrdinalIgnoreCase) &&
                entries.Length > 0 &&
                entries.All(entry => entry.Name.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase));
            if (isLegacyAdoptiumArchiveRecord)
            {
                return new JavaIntegrityVerification(
                    JavaIntegrityStatus.Unknown,
                    "The legacy Adoptium manifest only records the source archive hash.");
            }

            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (entry.Value.ValueKind != JsonValueKind.String)
                {
                    return new JavaIntegrityVerification(
                        JavaIntegrityStatus.Invalid,
                        $"Invalid SHA256 record for '{entry.Name}'.");
                }

                var expected = entry.Value.GetString();
                var filePath = GetSafeChildPath(runtimeDirectory, entry.Name);
                if (!File.Exists(filePath) || string.IsNullOrWhiteSpace(expected))
                {
                    return new JavaIntegrityVerification(
                        JavaIntegrityStatus.Invalid,
                        $"Missing runtime file '{entry.Name}'.");
                }

                var actual = await HashFileAsync(filePath, cancellationToken).ConfigureAwait(false);
                if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                {
                    return new JavaIntegrityVerification(
                        JavaIntegrityStatus.Invalid,
                        $"SHA256 mismatch for '{entry.Name}'.");
                }
            }

            return new JavaIntegrityVerification(JavaIntegrityStatus.Verified);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException
                                               or JsonException
                                               or InvalidDataException
                                               or UnauthorizedAccessException)
        {
            return new JavaIntegrityVerification(
                JavaIntegrityStatus.Unknown,
                exception.Message);
        }
    }

    private static string GetSafeChildPath(string rootPath, string relativePath)
    {
        var normalizedRoot = Path.GetFullPath(rootPath) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(
            rootPath,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Runtime integrity manifest escaped its directory.");
        }

        return fullPath;
    }

    private static async Task<string> HashFileAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var hash = SHA256.Create();
        var digest = await hash.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(digest);
    }
}
