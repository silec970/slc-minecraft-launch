using System.Text.Json;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Infrastructure.Mojang;

public sealed class MojangVersionCatalogService(
    IHttpClientFactory httpClientFactory,
    string versionManifestUrl) : IVanillaVersionCatalogService
{
    public async Task<IReadOnlyList<MinecraftReleaseDescriptor>> GetVersionsAsync(
        CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient();
        using var response = await client.GetAsync(
            versionManifestUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content
            .ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(
            stream,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        var root = document.RootElement;
        var latest = root.GetProperty("latest");
        var latestRelease = latest.GetProperty("release").GetString();
        var latestSnapshot = latest.GetProperty("snapshot").GetString();
        var versions = root.GetProperty("versions");

        return versions.EnumerateArray()
            .Select(version =>
            {
                var id = version.GetProperty("id").GetString()
                         ?? throw new InvalidDataException("Mojang version id is missing.");
                var type = version.GetProperty("type").GetString() ?? "unknown";
                var releaseTime = DateTimeOffset.TryParse(
                    version.GetProperty("releaseTime").GetString(),
                    out var timestamp)
                    ? timestamp
                    : DateTimeOffset.MinValue;
                return new MinecraftReleaseDescriptor(
                    id,
                    type,
                    releaseTime,
                    string.Equals(id, latestRelease, StringComparison.OrdinalIgnoreCase),
                    string.Equals(id, latestSnapshot, StringComparison.OrdinalIgnoreCase));
            })
            .OrderByDescending(version => version.ReleaseTime)
            .ToArray();
    }
}

