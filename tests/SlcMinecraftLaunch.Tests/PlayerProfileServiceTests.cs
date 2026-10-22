using SlcMinecraftLaunch.Application;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Tests;

public sealed class PlayerProfileServiceTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Save_RejectsEmptyPlayerName(string value)
    {
        var service = new PlayerProfileService(new MemoryStore());

        var result = await service.SavePlayerNameAsync(value);

        Assert.False(result.Succeeded);
        Assert.Equal("Settings.PlayerNameRequired", result.MessageKey);
    }

    [Fact]
    public async Task Save_TrimsAndPersistsUnicodePlayerName()
    {
        var store = new MemoryStore();
        var service = new PlayerProfileService(store);

        var result = await service.SavePlayerNameAsync("  玩家一  ");

        Assert.True(result.Succeeded);
        Assert.Equal("玩家一", result.PlayerName);
        Assert.Equal("玩家一", await service.GetPlayerNameAsync());
    }

    private sealed class MemoryStore : IPlayerProfileStore
    {
        private PlayerProfileSettings _settings = new();

        public Task<PlayerProfileSettings> LoadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_settings);

        public Task SaveAsync(
            PlayerProfileSettings settings,
            CancellationToken cancellationToken = default)
        {
            _settings = settings;
            return Task.CompletedTask;
        }
    }
}

