using System.Text.Json;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Infrastructure.Persistence;

public sealed class JsonDownloadTaskStore(string dataRoot) : IDownloadTaskStore
{
    private readonly string _path = Path.Combine(dataRoot, "download-tasks.json");
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public async Task<IReadOnlyList<DownloadTaskRecord>> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path))
        {
            return [];
        }

        try
        {
            await using var input = File.OpenRead(_path);
            return await JsonSerializer.DeserializeAsync<List<DownloadTaskRecord>>(
                       input,
                       cancellationToken: cancellationToken).ConfigureAwait(false)
                   ?? [];
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    public async Task SaveAsync(
        IReadOnlyList<DownloadTaskRecord> tasks,
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
                        tasks,
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
