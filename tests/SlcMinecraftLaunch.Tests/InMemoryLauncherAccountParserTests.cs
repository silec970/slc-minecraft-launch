using ProjBobcat.Class.Model.LauncherAccount;
using SlcMinecraftLaunch.Infrastructure.ProjBobcat;

namespace SlcMinecraftLaunch.Tests;

public sealed class InMemoryLauncherAccountParserTests
{
    [Fact]
    public void AddOrReplaceAccount_StoresOfflineProfileInMemory()
    {
        var parser = new InMemoryLauncherAccountParser(Guid.NewGuid());
        var account = new AccountModel
        {
            AccessToken = "memory-only",
            LocalId = "local",
            RemoteId = "remote",
            Type = "Offline",
            Username = "Player",
            MinecraftProfile = new AccountProfileModel
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = "Player"
            }
        };

        var added = parser.AddOrReplaceAccount("local", account, out var id);

        Assert.True(added);
        Assert.NotNull(id);
        Assert.Single(parser.LauncherAccount.Accounts!);
        Assert.Equal("local", parser.LauncherAccount.ActiveAccountLocalId);
    }
}
