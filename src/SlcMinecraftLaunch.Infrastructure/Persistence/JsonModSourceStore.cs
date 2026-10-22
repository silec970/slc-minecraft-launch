using System.Text.Json;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Infrastructure.Persistence;

public sealed class JsonModSourceStore(string dataRoot) : IModSourceStore
{
    private readonly string _path = Path.Combine(dataRoot, "mod-sources.json");
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public async Task<IReadOnlyDictionary<string, ModSourceMetadata>> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path))
        {
            return new Dictionary<string, ModSourceMetadata>(StringComparer.OrdinalIgnoreCase);
        }

        await using var input = File.OpenRead(_path);
        var values = await JsonSerializer.DeserializeAsync<Dictionary<string, ModSourceMetadata>>(
            input,
            cancellationToken: cancellationToken).ConfigureAwait(false);
        return values is null
            ? new Dictionary<string, ModSourceMetadata>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, ModSourceMetadata>(values, StringComparer.OrdinalIgnoreCase);
    }

    public async Task SaveAsync(
        IReadOnlyDictionary<string, ModSourceMetadata> sources,
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
                        sources,
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
