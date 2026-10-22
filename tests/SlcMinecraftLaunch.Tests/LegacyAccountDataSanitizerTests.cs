using SlcMinecraftLaunch.Infrastructure.Persistence;

namespace SlcMinecraftLaunch.Tests;

public sealed class LegacyAccountDataSanitizerTests
{
    [Fact]
    public async Task Sanitize_RemovesNestedTokenFields()
    {
        var root = Path.Combine(Path.GetTempPath(), $"slc-account-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "launcher_accounts.json");

        try
        {
            await File.WriteAllTextAsync(path, """
                {
                  "accounts": {
                    "local": {
                      "accessToken": "secret-access",
                      "refreshToken": "secret-refresh",
                      "username": "Player"
                    }
                  },
                  "activeAccountLocalId": "local",
                  "mojangClientToken": "client"
                }
                """);

            await new LegacyAccountDataSanitizer(root).SanitizeAsync();
            var result = await File.ReadAllTextAsync(path);

            Assert.DoesNotContain("secret-access", result);
            Assert.DoesNotContain("secret-refresh", result);
            Assert.Contains("\"username\": \"Player\"", result);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }
}
