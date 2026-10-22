using SlcMinecraftLaunch.Domain.Models;
using SlcMinecraftLaunch.Infrastructure.Persistence;

namespace SlcMinecraftLaunch.Tests;

public sealed class UiSettingsStoreTests
{
    [Fact]
    public async Task Store_RoundTripsDarkThemeAndMotionSettings()
    {
        var root = Path.Combine(Path.GetTempPath(), $"slc-ui-{Guid.NewGuid():N}");
        try
        {
            var store = new JsonUiSettingsStore(root);
            var expected = new UiSettings(
                UiTheme.Dark,
                "en-US",
                AnimationsEnabled: false,
                BackgroundEnabled: false,
                MusicEnabled: true,
                MusicVolume: 0.2);

            await store.SaveAsync(expected);
            var actual = await store.LoadAsync();

            Assert.Equal(expected, actual);
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
