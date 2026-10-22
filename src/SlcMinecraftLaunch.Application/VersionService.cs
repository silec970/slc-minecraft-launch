using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Application;

public sealed class VersionService(
    IGameRuntimeProvider runtimeProvider,
    IVersionPreferencesStore preferencesStore) : IVersionService
{
    private static readonly LoaderKind[] GroupOrder =
    [
        LoaderKind.Vanilla,
        LoaderKind.NeoForge,
        LoaderKind.Forge,
        LoaderKind.Fabric,
        LoaderKind.Quilt,
        LoaderKind.OptiFine,
        LoaderKind.LiteLoader,
        LoaderKind.Other
    ];

    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public async Task<GameVersionCatalog> GetCatalogAsync(CancellationToken cancellationToken = default)
    {
        var runtime = await runtimeProvider.GetAsync(cancellationToken).ConfigureAwait(false);
        var versions = await runtime.GetVersionsAsync(cancellationToken).ConfigureAwait(false);
        var preferences = await preferencesStore.LoadAsync(cancellationToken).ConfigureAwait(false);

        var entries = versions
            .Select(version =>
            {
                var preference = preferences.GetValueOrDefault(version.Id) ?? new VersionPreference();
                return new GameVersionEntry(version, preference.IsFavorite, preference.IsIsolated);
            })
            .ToArray();

        var groups = GroupOrder
            .Select(loaderKind => new GameVersionGroup(
                loaderKind,
                $"VersionGroup.{loaderKind}",
                entries
                    .Where(entry => entry.Version.LoaderKind == loaderKind)
                    .OrderByDescending(entry => entry.IsFavorite)
                    .ThenBy(entry => entry.Version.BaseVersion, VersionComparer.Instance)
                    .ThenBy(entry => entry.Version.Id, StringComparer.OrdinalIgnoreCase)
                    .ToArray()))
            .Where(group => group.Versions.Count > 0)
            .ToArray();

        return new GameVersionCatalog(runtime.RootPath, groups);
    }

    public Task SetFavoriteAsync(
        string versionId,
        bool isFavorite,
        CancellationToken cancellationToken = default)
    {
        return UpdatePreferenceAsync(
            versionId,
            preference => preference with { IsFavorite = isFavorite },
            cancellationToken);
    }

    public Task SetIsolationAsync(
        string versionId,
        bool isIsolated,
        CancellationToken cancellationToken = default)
    {
        return UpdatePreferenceAsync(
            versionId,
            preference => preference with { IsIsolated = isIsolated },
            cancellationToken);
    }

    private async Task UpdatePreferenceAsync(
        string versionId,
        Func<VersionPreference, VersionPreference> update,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(versionId);

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var runtime = await runtimeProvider.GetAsync(cancellationToken).ConfigureAwait(false);
            var versions = await runtime.GetVersionsAsync(cancellationToken).ConfigureAwait(false);
            if (!versions.Any(version =>
                    string.Equals(version.Id, versionId, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"Version '{versionId}' was not found.");
            }

            var preferences = new Dictionary<string, VersionPreference>(
                await preferencesStore.LoadAsync(cancellationToken).ConfigureAwait(false),
                StringComparer.OrdinalIgnoreCase);
            var current = preferences.GetValueOrDefault(versionId) ?? new VersionPreference();
            preferences[versionId] = update(current);

            await preferencesStore.SaveAsync(preferences, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private sealed class VersionComparer : IComparer<string>
    {
        public static VersionComparer Instance { get; } = new();

        public int Compare(string? x, string? y)
        {
            return NaturalVersionComparer.Compare(x, y);
        }
    }
}

internal static class NaturalVersionComparer
{
    public static int Compare(string? left, string? right)
    {
        if (string.Equals(left, right, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (left is null)
        {
            return -1;
        }

        if (right is null)
        {
            return 1;
        }

        var leftParts = left.Split(['.', '-', '+'], StringSplitOptions.RemoveEmptyEntries);
        var rightParts = right.Split(['.', '-', '+'], StringSplitOptions.RemoveEmptyEntries);
        var length = Math.Min(leftParts.Length, rightParts.Length);

        for (var index = 0; index < length; index++)
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

        return leftParts.Length.CompareTo(rightParts.Length);
    }
}
