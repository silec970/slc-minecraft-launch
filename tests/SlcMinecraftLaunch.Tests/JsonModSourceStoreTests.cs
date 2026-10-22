using SlcMinecraftLaunch.Domain.Models;
using SlcMinecraftLaunch.Infrastructure.Persistence;

namespace SlcMinecraftLaunch.Tests;

public sealed class JsonModSourceStoreTests
{
    [Fact]
    public async Task Store_RoundTripsProviderDependencyMetadata()
    {
        var root = Path.Combine(Path.GetTempPath(), $"slc-mod-source-{Guid.NewGuid():N}");
        try
        {
            var store = new JsonModSourceStore(root);
            var source = new ModSourceMetadata(
                "examplemod",
                ModProvider.Modrinth,
                "project-id",
                "version-id",
                [new ModDependencySource(
                    "fabric-api",
                    ModProvider.Modrinth,
                    "fabric-api-project",
                    null,
                    true,
                    ">=1.0")]);

            await store.SaveAsync(new Dictionary<string, ModSourceMetadata>
            {
                ["examplemod"] = source
            });
            var result = await store.LoadAsync();

            var actual = result["examplemod"];
            Assert.Equal(source.ModId, actual.ModId);
            Assert.Equal(source.Provider, actual.Provider);
            Assert.Equal(source.ProjectId, actual.ProjectId);
            Assert.Equal(source.Dependencies[0], actual.Dependencies[0]);
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
