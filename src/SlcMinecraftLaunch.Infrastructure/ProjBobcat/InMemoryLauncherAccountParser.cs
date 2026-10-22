using ProjBobcat.Class.Model.LauncherAccount;
using ProjBobcat.Interface;

namespace SlcMinecraftLaunch.Infrastructure.ProjBobcat;

public sealed class InMemoryLauncherAccountParser : ILauncherAccountParser
{
    private readonly Lock _lock = new();

    public InMemoryLauncherAccountParser(Guid clientToken)
    {
        LauncherAccount = new LauncherAccountModel
        {
            Accounts = [],
            MojangClientToken = clientToken.ToString("N")
        };
    }

    public LauncherAccountModel LauncherAccount { get; }

    public bool AddOrReplaceAccount(string uuid, AccountModel account, out Guid? id)
    {
        lock (_lock)
        {
            var existing = LauncherAccount.Accounts?
                .FirstOrDefault(pair =>
                    pair.Value.MinecraftProfile?.Id.Equals(
                        account.MinecraftProfile?.Id,
                        StringComparison.OrdinalIgnoreCase) == true);
            var accountId = existing is { Value: not null } entry &&
                            entry.Value.Id != Guid.Empty
                ? entry.Value.Id
                : Guid.NewGuid();
            account.Id = accountId;
            LauncherAccount.Accounts ??= [];
            LauncherAccount.Accounts[uuid] = account;
            LauncherAccount.ActiveAccountLocalId ??= uuid;
            id = accountId;
            return true;
        }
    }

    public bool RemoveAccount(Guid id)
    {
        lock (_lock)
        {
            var entry = LauncherAccount.Accounts?.FirstOrDefault(pair => pair.Value.Id == id);
            if (entry is null)
            {
                return false;
            }

            var removed = LauncherAccount.Accounts!.Remove(entry.Value.Key);
            if (LauncherAccount.ActiveAccountLocalId == entry.Value.Key)
            {
                LauncherAccount.ActiveAccountLocalId = null;
            }

            return removed;
        }
    }

    public KeyValuePair<string, AccountModel>? Find(Guid id)
    {
        lock (_lock)
        {
            return LauncherAccount.Accounts?.FirstOrDefault(pair => pair.Value.Id == id);
        }
    }

    public bool ActivateAccount(string uuid)
    {
        lock (_lock)
        {
            if (LauncherAccount.Accounts?.ContainsKey(uuid) != true)
            {
                return false;
            }

            LauncherAccount.ActiveAccountLocalId = uuid;
            return true;
        }
    }

    public void Save()
    {
        // Offline accounts are memory-only so credentials never reach disk.
    }
}

