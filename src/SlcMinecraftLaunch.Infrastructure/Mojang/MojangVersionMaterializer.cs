using System.Net.Http.Json;
using System.Text.Json;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Infrastructure.Mojang;

public sealed class MojangVersionMaterializer(
    IHttpClientFactory httpClientFactory,
    string versionManifestUrl) : IGameVersionMaterializer
{
    public async Task EnsureVersionAsync(
        string rootPath,
        string targetVersion,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var versionDirectory = Path.Combine(rootPath, "versions", targetVersion);
        var versionJsonPath = Path.Combine(versionDirectory, $"{targetVersion}.json");
        var clientJarPath = Path.Combine(versionDirectory, $"{targetVersion}.jar");

        if (File.Exists(versionJsonPath))
        {
            progress?.Report(new OperationProgress(
                SmokeTestStage.MaterializingVersion,
                "Progress.VersionAlreadyPresent",
                0.1));
            return;
        }

        Directory.CreateDirectory(versionDirectory);
        var client = httpClientFactory.CreateClient();

        progress?.Report(new OperationProgress(
            SmokeTestStage.MaterializingVersion,
            "Progress.FetchingVersionManifest",
            0.01));

        using var manifestResponse = await client.GetAsync(
            versionManifestUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        manifestResponse.EnsureSuccessStatusCode();

        await using var manifestStream = await manifestResponse.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        using var manifest = await JsonDocument.ParseAsync(
            manifestStream,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var versionMetadataUrl = FindVersionMetadataUrl(manifest.RootElement, targetVersion);
        await DownloadFileAsync(
            client,
            versionMetadataUrl,
            versionJsonPath,
            SmokeTestStage.MaterializingVersion,
            "Progress.DownloadingVersionJson",
            0.03,
            0.08,
            progress,
            cancellationToken).ConfigureAwait(false);

        if (!File.Exists(clientJarPath))
        {
            using var versionStream = File.OpenRead(versionJsonPath);
            using var versionDocument = await JsonDocument.ParseAsync(
                versionStream,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            var clientDownloadUrl = ReadClientDownloadUrl(versionDocument.RootElement);
            await DownloadFileAsync(
                client,
                clientDownloadUrl,
                clientJarPath,
                SmokeTestStage.MaterializingVersion,
                "Progress.DownloadingClientJar",
                0.08,
                0.1,
                progress,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static string FindVersionMetadataUrl(JsonElement manifest, string targetVersion)
    {
        if (!manifest.TryGetProperty("versions", out var versions) ||
            versions.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("The Mojang version manifest has no versions array.");
        }

        foreach (var version in versions.EnumerateArray())
        {
            if (version.TryGetProperty("id", out var id) &&
                string.Equals(id.GetString(), targetVersion, StringComparison.OrdinalIgnoreCase) &&
                version.TryGetProperty("url", out var url) &&
                !string.IsNullOrWhiteSpace(url.GetString()))
            {
                return url.GetString()!;
            }
        }

        throw new InvalidOperationException($"Minecraft version '{targetVersion}' is not present in the manifest.");
    }

    private static string ReadClientDownloadUrl(JsonElement version)
    {
        if (version.TryGetProperty("downloads", out var downloads) &&
            downloads.TryGetProperty("client", out var client) &&
            client.TryGetProperty("url", out var url) &&
            !string.IsNullOrWhiteSpace(url.GetString()))
        {
            return url.GetString()!;
        }

        throw new InvalidDataException("The version JSON has no client download URL.");
    }

    private static async Task DownloadFileAsync(
        HttpClient client,
        string url,
        string destinationPath,
        SmokeTestStage stage,
        string messageKey,
        double startFraction,
        double endFraction,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(
            url,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;
        var temporaryPath = $"{destinationPath}.{Guid.NewGuid():N}.tmp";

        try
        {
            {
                await using var source = await response.Content
                    .ReadAsStreamAsync(cancellationToken)
                    .ConfigureAwait(false);
                await using var destination = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);

                var buffer = new byte[81920];
                long received = 0;

                while (true)
                {
                    var read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                    received += read;

                    var ratio = totalBytes is > 0
                        ? (double)received / totalBytes.Value
                        : 0;
                    progress?.Report(new OperationProgress(
                        stage,
                        messageKey,
                        startFraction + ((endFraction - startFraction) * OperationProgress.NormalizeFraction(ratio)),
                        received,
                        totalBytes));
                }
            }

            File.Move(temporaryPath, destinationPath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}

