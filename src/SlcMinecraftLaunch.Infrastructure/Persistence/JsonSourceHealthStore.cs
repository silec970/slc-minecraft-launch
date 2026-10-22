using System.Text.Json;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Infrastructure.Persistence;

public sealed class JsonSourceHealthStore(string dataRoot)
{
    private readonly string _path = Path.Combine(dataRoot, "download-source-health.json");
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public async Task<IReadOnlyDictionary<string, DownloadSourceScore>> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path))
        {
            return new Dictionary<string, DownloadSourceScore>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            await using var input = File.OpenRead(_path);
            var scores = await JsonSerializer.DeserializeAsync<List<DownloadSourceScore>>(
                             input,
                             cancellationToken: cancellationToken).ConfigureAwait(false)
                         ?? [];
            return scores.ToDictionary(
                score => score.SourceRoot,
                StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return new Dictionary<string, DownloadSourceScore>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public async Task SaveAsync(
        IReadOnlyCollection<DownloadSourceScore> scores,
        CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(dataRoot);
            var temporaryPath = $"{_path}.{Guid.NewGuid():N}.tmp";
            try
            {
                await using (var output = File.Create(temporaryPath))
                {
                    await JsonSerializer.SerializeAsync(
                        output,
                        scores.OrderBy(score => score.SourceRoot, StringComparer.OrdinalIgnoreCase),
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
