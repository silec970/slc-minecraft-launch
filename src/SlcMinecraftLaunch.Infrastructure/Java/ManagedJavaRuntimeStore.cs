using System.Text.Json;
using System.Text.RegularExpressions;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;
using SlcMinecraftLaunch.Infrastructure.Configuration;

namespace SlcMinecraftLaunch.Infrastructure.Java;

public sealed partial class ManagedJavaRuntimeStore(
    PortableLauncherPaths paths,
    JavaRuntimeIntegrityVerifier integrityVerifier) : IJavaRuntimeStore
{
    private string RuntimesRoot => Path.Combine(paths.GameRoot, "runtimes");

    public Task<IReadOnlyList<JavaCandidate>> DiscoverAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.Run<IReadOnlyList<JavaCandidate>>(
            () => Discover(cancellationToken),
            cancellationToken);
    }

    private IReadOnlyList<JavaCandidate> Discover(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!Directory.Exists(RuntimesRoot))
        {
            return [];
        }

        var candidates = new List<JavaCandidate>();
        foreach (var executablePath in Directory.EnumerateFiles(
                     RuntimesRoot,
                     "java.exe",
                     SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.Equals(
                    Path.GetFileName(Path.GetDirectoryName(executablePath)),
                    "bin",
                    StringComparison.OrdinalIgnoreCase) ||
                executablePath.Contains(".tmp", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var runtimeDirectory = GetRuntimeDirectory(executablePath);
            if (runtimeDirectory is null ||
                !File.Exists(Path.Combine(runtimeDirectory, ".slc-ready")))
            {
                continue;
            }

            var metadata = TryReadMetadata(runtimeDirectory);
            candidates.Add(new JavaCandidate(
                executablePath,
                metadata?.RequiredMajorVersion ?? InferMajorVersion(runtimeDirectory),
                metadata?.Source ?? InferSource(runtimeDirectory),
                metadata is null
                    ? JavaIntegrityStatus.Unknown
                    : JavaIntegrityStatus.Recorded,
                Path.GetRelativePath(RuntimesRoot, runtimeDirectory)));
        }

        return candidates
            .OrderBy(candidate => candidate.MajorVersion)
            .ThenBy(candidate => candidate.ExecutablePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<JavaCandidate?> ReverifyAsync(
        string executablePath,
        CancellationToken cancellationToken = default)
    {
        var runtimeDirectory = GetRuntimeDirectory(executablePath);
        if (runtimeDirectory is null || !Directory.Exists(runtimeDirectory))
        {
            return null;
        }

        var verification = await integrityVerifier
            .VerifyAsync(runtimeDirectory, cancellationToken)
            .ConfigureAwait(false);
        var metadata = TryReadMetadata(runtimeDirectory);
        return new JavaCandidate(
            Path.GetFullPath(executablePath),
            metadata?.RequiredMajorVersion ?? InferMajorVersion(runtimeDirectory),
            metadata?.Source ?? InferSource(runtimeDirectory),
            verification.Status,
            Path.GetRelativePath(RuntimesRoot, runtimeDirectory),
            verification.Detail);
    }

    public async Task<bool> DeleteManagedAsync(
        string executablePath,
        CancellationToken cancellationToken = default)
    {
        var runtimeDirectory = GetRuntimeDirectory(executablePath);
        if (runtimeDirectory is null ||
            !Directory.Exists(runtimeDirectory) ||
            !File.Exists(Path.Combine(runtimeDirectory, ".slc-ready")) ||
            !File.Exists(Path.Combine(runtimeDirectory, ".integrity.json")))
        {
            return false;
        }

        await Task.Run(
            () => Directory.Delete(runtimeDirectory, true),
            cancellationToken).ConfigureAwait(false);
        return true;
    }

    private string? GetRuntimeDirectory(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath) ||
            !string.Equals(Path.GetFileName(executablePath), "java.exe", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var fullExecutablePath = Path.GetFullPath(executablePath);
        var binDirectory = Path.GetDirectoryName(fullExecutablePath);
        if (binDirectory is null ||
            !string.Equals(Path.GetFileName(binDirectory), "bin", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var runtimeDirectory = Path.GetDirectoryName(binDirectory);
        if (runtimeDirectory is null)
        {
            return null;
        }

        var normalizedRoot = Path.GetFullPath(RuntimesRoot) + Path.DirectorySeparatorChar;
        var normalizedRuntime = Path.GetFullPath(runtimeDirectory);
        return normalizedRuntime.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(
                   Path.TrimEndingDirectorySeparator(normalizedRuntime),
                   Path.TrimEndingDirectorySeparator(RuntimesRoot),
                   StringComparison.OrdinalIgnoreCase)
            ? normalizedRuntime
            : null;
    }

    private static RuntimeMetadata? TryReadMetadata(string runtimeDirectory)
    {
        var manifestPath = Path.Combine(runtimeDirectory, ".integrity.json");
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var root = document.RootElement;
            if (!root.TryGetProperty("files", out _))
            {
                return null;
            }

            var sourceName = root.TryGetProperty("source", out var sourceElement)
                ? sourceElement.GetString()
                : null;
            var source = string.Equals(sourceName, "mojang", StringComparison.OrdinalIgnoreCase)
                ? JavaRuntimeSource.Mojang
                : string.Equals(sourceName, "adoptium", StringComparison.OrdinalIgnoreCase)
                    ? JavaRuntimeSource.Adoptium
                    : InferSource(runtimeDirectory);
            var requiredMajor = root.TryGetProperty("requiredMajorVersion", out var majorElement) &&
                                majorElement.TryGetInt32(out var parsedMajor)
                ? parsedMajor
                : InferMajorVersion(runtimeDirectory);

            return new RuntimeMetadata(source, requiredMajor);
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static JavaRuntimeSource InferSource(string runtimeDirectory)
    {
        return runtimeDirectory.Split(
                   Path.DirectorySeparatorChar,
                   Path.AltDirectorySeparatorChar)
               .Contains("adoptium", StringComparer.OrdinalIgnoreCase)
            ? JavaRuntimeSource.Adoptium
            : JavaRuntimeSource.Mojang;
    }

    private static int InferMajorVersion(string runtimeDirectory)
    {
        var directoryName = Path.GetFileName(runtimeDirectory);
        if (int.TryParse(directoryName, out var directMajor))
        {
            return directMajor;
        }

        var match = TemurinMajorRegex().Match(directoryName);
        return match.Success && int.TryParse(match.Groups["major"].Value, out var major)
            ? major
            : 0;
    }

    [GeneratedRegex(@"^temurin-(?<major>\d+)-", RegexOptions.IgnoreCase)]
    private static partial Regex TemurinMajorRegex();

    private sealed record RuntimeMetadata(
        JavaRuntimeSource Source,
        int RequiredMajorVersion);
}
