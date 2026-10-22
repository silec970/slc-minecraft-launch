using System.Text.Json;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Infrastructure.Persistence;

public sealed class JsonVersionPreferencesStore(string dataRoot) : IVersionPreferencesStore
{
    private readonly string _preferencesPath = Path.Combine(dataRoot, "version-preferences.json");
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public async Task<IReadOnlyDictionary<string, VersionPreference>> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_preferencesPath))
        {
            return new Dictionary<string, VersionPreference>(StringComparer.OrdinalIgnoreCase);
        }

        await using var input = File.OpenRead(_preferencesPath);
        var preferences = await JsonSerializer.DeserializeAsync<Dictionary<string, VersionPreference>>(
            input,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return preferences is null
            ? new Dictionary<string, VersionPreference>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, VersionPreference>(preferences, StringComparer.OrdinalIgnoreCase);
    }

    public async Task SaveAsync(
        IReadOnlyDictionary<string, VersionPreference> preferences,
        CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(dataRoot);
            var temporaryPath = $"{_preferencesPath}.{Guid.NewGuid():N}.tmp";

            try
            {
                {
                    await using var output = File.Create(temporaryPath);
                    await JsonSerializer.SerializeAsync(
                        output,
                        preferences,
                        new JsonSerializerOptions { WriteIndented = true },
                        cancellationToken).ConfigureAwait(false);
                }

                File.Move(temporaryPath, _preferencesPath, true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }
}

