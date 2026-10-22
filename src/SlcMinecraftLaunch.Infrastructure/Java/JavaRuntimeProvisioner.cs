using System.IO.Compression;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;
using SlcMinecraftLaunch.Infrastructure.Configuration;

namespace SlcMinecraftLaunch.Infrastructure.Java;

public sealed class JavaRuntimeProvisioner(
    PortableLauncherPaths paths,
    IHttpClientFactory httpClientFactory,
    string mojangRuntimeManifestUrl,
    string adoptiumAssetsUrlTemplate) : IJavaRuntimeProvisioner
{
    private readonly SemaphoreSlim _installLock = new(1, 1);

    public async Task<JavaRuntimeProvisionOutcome> EnsureAsync(
        int requiredMajorVersion,
        string? mojangComponent,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await _installLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var errors = new List<string>();

            if (!string.IsNullOrWhiteSpace(mojangComponent))
            {
                try
                {
                    var executable = await ProvisionMojangAsync(
                        requiredMajorVersion,
                        mojangComponent,
                        progress,
                        cancellationToken).ConfigureAwait(false);
                    return new JavaRuntimeProvisionOutcome(
                        true,
                        executable,
                        "Java.MojangRuntimeReady");
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    errors.Add($"Mojang: {exception}");
                }
            }

            try
            {
                var executable = await ProvisionAdoptiumAsync(
                    requiredMajorVersion,
                    progress,
                    cancellationToken).ConfigureAwait(false);
                return new JavaRuntimeProvisionOutcome(
                    true,
                    executable,
                    "Java.AdoptiumRuntimeReady");
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                errors.Add($"Adoptium: {exception}");
            }

            progress?.Report(new OperationProgress(
                SmokeTestStage.Failed,
                "Java.ProvisionFailed",
                1));
            return new JavaRuntimeProvisionOutcome(
                false,
                null,
                "Java.ProvisionFailed",
                string.Join(Environment.NewLine + Environment.NewLine, errors));
        }
        finally
        {
            _installLock.Release();
        }
    }

    private async Task<string> ProvisionMojangAsync(
        int requiredMajorVersion,
        string component,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        var architecture = GetDownloadArchitecture();
        var platformKey = $"windows-{architecture}";
        var finalDirectory = Path.Combine(
            paths.GameRoot,
            "runtimes",
            "mojang",
            component,
            requiredMajorVersion.ToString());
        var executable = Path.Combine(finalDirectory, "bin", "java.exe");
        if (IsReady(finalDirectory, executable))
        {
            return executable;
        }

        var client = httpClientFactory.CreateClient();
        using var catalogResponse = await client.GetAsync(
            mojangRuntimeManifestUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        catalogResponse.EnsureSuccessStatusCode();
        await using var catalogStream = await catalogResponse.Content
            .ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var catalog = await JsonDocument.ParseAsync(
            catalogStream,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var componentManifestUrl = FindMojangComponentManifest(
            catalog.RootElement,
            platformKey,
            component);
        using var manifestResponse = await client.GetAsync(
            componentManifestUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        manifestResponse.EnsureSuccessStatusCode();
        await using var manifestStream = await manifestResponse.Content
            .ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var manifest = await JsonDocument.ParseAsync(
            manifestStream,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var files = manifest.RootElement.GetProperty("files");
        var fileEntries = files.EnumerateObject()
            .Where(entry => entry.Value.GetProperty("type").GetString() == "file")
            .ToArray();
        var temporaryDirectory = $"{finalDirectory}.{Guid.NewGuid():N}.tmp";
        Directory.CreateDirectory(temporaryDirectory);

        try
        {
            var sha256ByFile = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var completed = 0;

            foreach (var entry in files.EnumerateObject())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relativePath = entry.Name.Replace('/', Path.DirectorySeparatorChar);
                var destinationPath = GetSafeChildPath(temporaryDirectory, relativePath);
                var type = entry.Value.GetProperty("type").GetString();

                if (type == "directory")
                {
                    Directory.CreateDirectory(destinationPath);
                    continue;
                }

                if (type != "file")
                {
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                var download = entry.Value.GetProperty("downloads").GetProperty("raw");
                var url = download.GetProperty("url").GetString()
                          ?? throw new InvalidDataException($"Missing download URL for {entry.Name}.");
                var expectedSha1 = download.GetProperty("sha1").GetString()
                                   ?? throw new InvalidDataException($"Missing SHA1 for {entry.Name}.");

                await DownloadFileAsync(client, url, destinationPath, cancellationToken).ConfigureAwait(false);

                var actualSha1 = await HashFileAsync(destinationPath, SHA1.Create, cancellationToken)
                    .ConfigureAwait(false);
                if (!string.Equals(actualSha1, expectedSha1, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"Mojang SHA1 mismatch for runtime file '{entry.Name}'.");
                }

                sha256ByFile[entry.Name] = await HashFileAsync(
                    destinationPath,
                    SHA256.Create,
                    cancellationToken).ConfigureAwait(false);
                completed++;

                progress?.Report(new OperationProgress(
                    SmokeTestStage.DownloadingJava,
                    "Progress.DownloadingMojangJava",
                    0.15 + (0.7 * OperationProgress.NormalizeFraction(
                        fileEntries.Length == 0 ? 1 : (double)completed / fileEntries.Length)),
                    Detail: component));
            }

            await WriteIntegrityManifestAsync(
                temporaryDirectory,
                "mojang",
                component,
                sha256ByFile,
                cancellationToken).ConfigureAwait(false);
            var temporaryExecutable = Path.Combine(temporaryDirectory, "bin", "java.exe");
            if (!File.Exists(temporaryExecutable))
            {
                throw new InvalidDataException("The Mojang runtime doesn't contain bin/java.exe.");
            }

            await ReplaceDirectoryAsync(
                temporaryDirectory,
                finalDirectory,
                cancellationToken).ConfigureAwait(false);
            return executable;
        }
        finally
        {
            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, true);
            }
        }
    }

    private async Task<string> ProvisionAdoptiumAsync(
        int requiredMajorVersion,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        var architecture = GetDownloadArchitecture();
        var finalDirectory = Path.Combine(
            paths.GameRoot,
            "runtimes",
            "adoptium",
            $"temurin-{requiredMajorVersion}-{architecture}");
        var executable = Path.Combine(finalDirectory, "bin", "java.exe");
        if (IsReady(finalDirectory, executable))
        {
            return executable;
        }

        var client = httpClientFactory.CreateClient();
        var assetsUrl = adoptiumAssetsUrlTemplate
            .Replace("{major}", requiredMajorVersion.ToString(), StringComparison.Ordinal)
            .Replace("{arch}", architecture, StringComparison.Ordinal);
        var assets = await client.GetFromJsonAsync<JsonElement[]>(
            assetsUrl,
            cancellationToken).ConfigureAwait(false);
        var package = assets?
            .FirstOrDefault()
            .GetProperty("binary")
            .GetProperty("package");
        if (package is null || package.Value.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException("Adoptium returned no Windows JDK package.");
        }

        var downloadUrl = package.Value.GetProperty("link").GetString()
                          ?? throw new InvalidDataException("Adoptium returned no download URL.");
        var expectedSha256 = package.Value.GetProperty("checksum").GetString()
                             ?? throw new InvalidDataException("Adoptium returned no SHA256.");
        var archiveName = package.Value.GetProperty("name").GetString() ?? "temurin.zip";

        var temporaryRoot = $"{finalDirectory}.{Guid.NewGuid():N}.tmp";
        Directory.CreateDirectory(temporaryRoot);
        var archivePath = Path.Combine(temporaryRoot, archiveName);
        var extractPath = Path.Combine(temporaryRoot, "extracted");

        try
        {
            await DownloadFileAsync(client, downloadUrl, archivePath, cancellationToken).ConfigureAwait(false);
            var actualSha256 = await HashFileAsync(archivePath, SHA256.Create, cancellationToken)
                .ConfigureAwait(false);
            if (!string.Equals(actualSha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("Adoptium SHA256 validation failed.");
            }

            progress?.Report(new OperationProgress(
                SmokeTestStage.DownloadingJava,
                "Progress.ExtractingJava",
                0.88,
                Detail: archiveName));
            await ZipFile.ExtractToDirectoryAsync(
                archivePath,
                extractPath,
                cancellationToken).ConfigureAwait(false);
            var extractedExecutable = Directory
                .EnumerateFiles(extractPath, "java.exe", SearchOption.AllDirectories)
                .FirstOrDefault(path => path.EndsWith(
                    Path.Combine("bin", "java.exe"),
                    StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException("The Adoptium archive doesn't contain bin/java.exe.");
            var extractedRoot = Path.GetDirectoryName(Path.GetDirectoryName(extractedExecutable))!;

            await WriteIntegrityManifestAsync(
                extractedRoot,
                "adoptium",
                $"temurin-{requiredMajorVersion}",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [archiveName + ".sha256"] = actualSha256
                },
                cancellationToken).ConfigureAwait(false);

            await ReplaceDirectoryAsync(
                extractedRoot,
                finalDirectory,
                cancellationToken).ConfigureAwait(false);
            return executable;
        }
        finally
        {
            if (Directory.Exists(temporaryRoot))
            {
                Directory.Delete(temporaryRoot, true);
            }
        }
    }

    private static string FindMojangComponentManifest(
        JsonElement catalog,
        string platformKey,
        string component)
    {
        if (!catalog.TryGetProperty(platformKey, out var platform) ||
            !platform.TryGetProperty(component, out var componentEntries) ||
            componentEntries.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException(
                $"Mojang doesn't provide component '{component}' for platform '{platformKey}'.");
        }

        foreach (var entry in componentEntries.EnumerateArray())
        {
            if (entry.TryGetProperty("manifest", out var manifest) &&
                manifest.TryGetProperty("url", out var url) &&
                !string.IsNullOrWhiteSpace(url.GetString()))
            {
                return url.GetString()!;
            }
        }

        throw new InvalidDataException($"Mojang component '{component}' has no runtime manifest.");
    }

    private static async Task DownloadFileAsync(
        HttpClient client,
        string url,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(
            url,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var source = await response.Content
            .ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using var destination = new FileStream(
            destinationPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await source.CopyToAsync(destination, 81920, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string> HashFileAsync(
        string filePath,
        Func<HashAlgorithm> createHash,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var hash = createHash();
        var digest = await hash.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(digest);
    }

    private static string GetSafeChildPath(string rootPath, string relativePath)
    {
        var normalizedRoot = Path.GetFullPath(rootPath) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(rootPath, relativePath));
        if (!fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Runtime manifest attempted to escape the target directory.");
        }

        return fullPath;
    }

    private static bool IsReady(string directory, string executable)
    {
        return Directory.Exists(directory) &&
               File.Exists(Path.Combine(directory, ".slc-ready")) &&
               File.Exists(executable);
    }

    private static async Task ReplaceDirectoryAsync(
        string source,
        string destination,
        CancellationToken cancellationToken)
    {
        if (Directory.Exists(destination))
        {
            Directory.Delete(destination, true);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await File.WriteAllTextAsync(
            Path.Combine(source, ".slc-ready"),
            DateTimeOffset.UtcNow.ToString("O"),
            cancellationToken).ConfigureAwait(false);
        Directory.Move(source, destination);
    }

    private static Task WriteIntegrityManifestAsync(
        string root,
        string source,
        string component,
        IReadOnlyDictionary<string, string> fileHashes,
        CancellationToken cancellationToken)
    {
        return File.WriteAllTextAsync(
            Path.Combine(root, ".integrity.json"),
            JsonSerializer.Serialize(new
            {
                source,
                component,
                verifiedAt = DateTimeOffset.UtcNow,
                files = fileHashes
            }, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
    }

    private static string GetDownloadArchitecture()
    {
        return RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            var architecture => throw new PlatformNotSupportedException(
                $"Java runtime downloads aren't supported for {architecture}.")
        };
    }
}
