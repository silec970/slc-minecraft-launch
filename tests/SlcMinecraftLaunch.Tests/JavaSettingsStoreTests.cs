using SlcMinecraftLaunch.Domain.Models;
using SlcMinecraftLaunch.Infrastructure.Persistence;

namespace SlcMinecraftLaunch.Tests;

public sealed class JavaSettingsStoreTests
{
    [Fact]
    public async Task Store_RoundTripsAndClearsManualJavaPath()
    {
        var root = Path.Combine(Path.GetTempPath(), $"slc-java-settings-{Guid.NewGuid():N}");
        try
        {
            var store = new JsonJavaSettingsStore(root);
            var expectedPath = @"C:\Java\21\bin\java.exe";

            await store.SaveAsync(new JavaSettings(expectedPath));
            var loaded = await store.LoadAsync();
            Assert.Equal(expectedPath, loaded.ManualExecutablePath);

            await store.SaveAsync(new JavaSettings());
            Assert.Null((await store.LoadAsync()).ManualExecutablePath);
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
