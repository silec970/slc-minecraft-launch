using System.Text.Json;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Infrastructure.Persistence;

public sealed class JsonUiSettingsStore(string dataRoot) : IUiSettingsStore
{
    private readonly string _path = Path.Combine(dataRoot, "ui-settings.json");
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public async Task<UiSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path))
        {
            return new UiSettings();
        }

        await using var input = File.OpenRead(_path);
        return await JsonSerializer.DeserializeAsync<UiSettings>(
                   input,
                   cancellationToken: cancellationToken).ConfigureAwait(false)
               ?? new UiSettings();
    }

    public async Task SaveAsync(
        UiSettings settings,
        CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(dataRoot);
            var temporaryPath = $"{_path}.{Guid.NewGuid():N}.tmp";
            try
            {
                {
                    await using var output = File.Create(temporaryPath);
                    await JsonSerializer.SerializeAsync(
                        output,
                        settings,
                        new JsonSerializerOptions { WriteIndented = true },
                        cancellationToken).ConfigureAwait(false);
                }

                File.Move(temporaryPath, _path, true);
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
