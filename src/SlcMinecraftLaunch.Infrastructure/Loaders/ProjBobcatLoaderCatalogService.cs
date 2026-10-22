using System.Text.Json;
using System.Xml.Linq;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Infrastructure.Loaders;

public sealed class ProjBobcatLoaderCatalogService(
    IHttpClientFactory httpClientFactory,
    LoaderSourceOptions sourceOptions) : ILoaderCatalogService
{
    public async Task<IReadOnlyList<LoaderVersionOption>> GetVersionsAsync(
        string minecraftVersion,
        LoaderKind loaderKind,
        CancellationToken cancellationToken = default)
    {
        return loaderKind switch
        {
            LoaderKind.Fabric => await GetFabricVersionsAsync(
                minecraftVersion,
                cancellationToken).ConfigureAwait(false),
            LoaderKind.Forge => await GetForgeVersionsAsync(
                minecraftVersion,
                cancellationToken).ConfigureAwait(false),
            LoaderKind.NeoForge => await GetNeoForgeVersionsAsync(
                minecraftVersion,
                cancellationToken).ConfigureAwait(false),
            LoaderKind.OptiFine => await GetOptiFineVersionsAsync(
                minecraftVersion,
                cancellationToken).ConfigureAwait(false),
            _ => Array.Empty<LoaderVersionOption>()
        };
    }

    private async Task<IReadOnlyList<LoaderVersionOption>> GetFabricVersionsAsync(
        string minecraftVersion,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient();
        var url = sourceOptions.FabricVersionsUrlTemplate.Replace(
            "{minecraft}",
            minecraftVersion,
            StringComparison.Ordinal);
        await using var stream = await client.GetStreamAsync(url, cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(
            stream,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return document.RootElement.EnumerateArray()
            .Select(item => item.GetProperty("loader"))
            .Select(loader => new LoaderVersionOption(
                loader.GetProperty("version").GetString()
                ?? throw new InvalidDataException("Fabric loader version is missing."),
                loader.TryGetProperty("stable", out var stable) && stable.GetBoolean(),
                $"Fabric {loader.GetProperty("version").GetString()}"))
            .OrderByDescending(option => option.Version, LoaderVersionComparer.Instance)
            .ToArray();
    }

    private async Task<IReadOnlyList<LoaderVersionOption>> GetForgeVersionsAsync(
        string minecraftVersion,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient();
        var xml = await client.GetStringAsync(sourceOptions.ForgeMetadataUrl, cancellationToken)
            .ConfigureAwait(false);
        var prefix = $"{minecraftVersion}-";

        return XDocument.Parse(xml)
            .Descendants("version")
            .Select(element => element.Value)
            .Where(version => version.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(version => version[prefix.Length..])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(version => new LoaderVersionOption(version, true, $"Forge {version}"))
            .OrderByDescending(option => option.Version, LoaderVersionComparer.Instance)
            .ToArray();
    }

    private async Task<IReadOnlyList<LoaderVersionOption>> GetNeoForgeVersionsAsync(
        string minecraftVersion,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient();
        var xml = await client.GetStringAsync(sourceOptions.NeoForgeMetadataUrl, cancellationToken)
            .ConfigureAwait(false);
        var prefix = GetNeoForgePrefix(minecraftVersion);

        return XDocument.Parse(xml)
            .Descendants("version")
            .Select(element => element.Value)
            .Where(version => version.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(version => new LoaderVersionOption(version, true, $"NeoForge {version}"))
            .OrderByDescending(option => option.Version, LoaderVersionComparer.Instance)
            .ToArray();
    }

    private async Task<IReadOnlyList<LoaderVersionOption>> GetOptiFineVersionsAsync(
        string minecraftVersion,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient();
        var url = sourceOptions.OptiFineListUrlTemplate.Replace(
            "{minecraft}",
            minecraftVersion,
            StringComparison.Ordinal);
        await using var stream = await client.GetStreamAsync(url, cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(
            stream,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return document.RootElement.EnumerateArray()
            .Select(item =>
            {
                var type = item.GetProperty("type").GetString() ?? string.Empty;
                var patch = item.GetProperty("patch").GetString() ?? string.Empty;
                var filename = item.GetProperty("filename").GetString() ?? string.Empty;
                return new LoaderVersionOption(
                    $"{type}_{patch}".Trim('_'),
                    !patch.StartsWith("pre", StringComparison.OrdinalIgnoreCase),
                    filename);
            })
            .Where(option => !string.IsNullOrWhiteSpace(option.Version))
            .ToArray();
    }

    private static string GetNeoForgePrefix(string minecraftVersion)
    {
        var parts = minecraftVersion.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 3 && parts[0] == "1")
        {
            return $"{parts[1]}.{parts[2]}.";
        }

        if (parts.Length >= 2)
        {
            return $"{parts[0]}.{parts[1]}.";
        }

        throw new InvalidDataException($"Unsupported Minecraft version '{minecraftVersion}'.");
    }

    private sealed class LoaderVersionComparer : IComparer<string>
    {
        public static LoaderVersionComparer Instance { get; } = new();

        public int Compare(string? left, string? right)
        {
            var leftParts = Split(left);
            var rightParts = Split(right);
            for (var index = 0; index < Math.Min(leftParts.Count, rightParts.Count); index++)
            {
                var leftIsNumber = int.TryParse(leftParts[index], out var leftNumber);
                var rightIsNumber = int.TryParse(rightParts[index], out var rightNumber);
                var comparison = leftIsNumber && rightIsNumber
                    ? leftNumber.CompareTo(rightNumber)
                    : string.Compare(leftParts[index], rightParts[index], StringComparison.OrdinalIgnoreCase);
                if (comparison != 0)
                {
                    return comparison;
                }
            }

            return leftParts.Count.CompareTo(rightParts.Count);
        }

        private static IReadOnlyList<string> Split(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? Array.Empty<string>()
                : value.Split(['.', '-', '_', '+'], StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
