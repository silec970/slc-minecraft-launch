using System.Text.Json;
using SlcMinecraftLaunch.Domain.Abstractions;

namespace SlcMinecraftLaunch.Infrastructure.Persistence;

public sealed class JsonCommandResultWriter : ICommandResultWriter
{
    public async Task WriteAsync(
        string dataRoot,
        string fileName,
        object value,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(dataRoot);
        var outputPath = Path.Combine(dataRoot, fileName);
        var temporaryPath = $"{outputPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var output = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(
                    output,
                    value,
                    new JsonSerializerOptions { WriteIndented = true },
                    cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, outputPath, true);
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
