using System.Text.Json;
using System.Text.Json.Nodes;
using ProjBobcat.Class.Helper;

namespace SlcMinecraftLaunch.Infrastructure.Persistence;

public sealed class LegacyAccountDataSanitizer(string rootPath)
{
    private static readonly string[] TokenPropertyNames =
    [
        "accessToken",
        "refreshToken",
        "idToken",
        "access_token",
        "refresh_token",
        "id_token"
    ];

    public async Task SanitizeAsync(CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(rootPath, GamePathHelper.GetLauncherAccountPath());
        if (!File.Exists(path))
        {
            return;
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        if (JsonNode.Parse(json) is not JsonObject root)
        {
            return;
        }

        RemoveTokenProperties(root);
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            {
                await using var output = File.Create(temporaryPath);
                await JsonSerializer.SerializeAsync(
                    output,
                    root,
                    new JsonSerializerOptions { WriteIndented = true },
                    cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, path, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static void RemoveTokenProperties(JsonNode node)
    {
        if (node is JsonObject jsonObject)
        {
            foreach (var propertyName in TokenPropertyNames)
            {
                jsonObject.Remove(propertyName);
            }

            foreach (var child in jsonObject.ToArray())
            {
                if (child.Value is not null)
                {
                    RemoveTokenProperties(child.Value);
                }
            }
        }
        else if (node is JsonArray jsonArray)
        {
            foreach (var child in jsonArray)
            {
                if (child is not null)
                {
                    RemoveTokenProperties(child);
                }
            }
        }
    }
}
