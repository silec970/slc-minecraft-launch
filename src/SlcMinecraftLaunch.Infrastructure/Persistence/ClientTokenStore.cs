using System.Text.Json;

namespace SlcMinecraftLaunch.Infrastructure.Persistence;

public sealed class ClientTokenStore(string dataRoot)
{
    private readonly string _tokenPath = Path.Combine(dataRoot, "client-token.json");

    public async Task<Guid> LoadOrCreateAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(_tokenPath))
        {
            await using var input = File.OpenRead(_tokenPath);
            var stored = await JsonSerializer.DeserializeAsync<ClientTokenRecord>(
                input,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            if (stored?.ClientToken is { } existing && existing != Guid.Empty)
            {
                return existing;
            }
        }

        var clientToken = Guid.NewGuid();
        Directory.CreateDirectory(dataRoot);

        var temporaryPath = $"{_tokenPath}.{Guid.NewGuid():N}.tmp";
        await using (var output = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(
                output,
                new ClientTokenRecord(clientToken),
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        File.Move(temporaryPath, _tokenPath, true);
        return clientToken;
    }

    private sealed record ClientTokenRecord(Guid ClientToken);
}

