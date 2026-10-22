using System.Globalization;
using System.Text;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Application;

public sealed class PlayerProfileService(IPlayerProfileStore profileStore) : IPlayerProfileService
{
    public async Task<string> GetPlayerNameAsync(CancellationToken cancellationToken = default)
    {
        var settings = await profileStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        return settings.PlayerName.Trim();
    }

    public async Task<PlayerProfileSaveResult> SavePlayerNameAsync(
        string playerName,
        CancellationToken cancellationToken = default)
    {
        var normalized = playerName.Trim();
        if (normalized.Length == 0)
        {
            return new PlayerProfileSaveResult(false, "Settings.PlayerNameRequired", normalized);
        }

        var runes = normalized.EnumerateRunes().ToArray();
        if (runes.Length > 16)
        {
            return new PlayerProfileSaveResult(false, "Settings.PlayerNameTooLong", normalized);
        }

        if (runes.Any(rune => Rune.GetUnicodeCategory(rune) == UnicodeCategory.Control))
        {
            return new PlayerProfileSaveResult(false, "Settings.PlayerNameInvalid", normalized);
        }

        await profileStore.SaveAsync(
            new PlayerProfileSettings(normalized),
            cancellationToken).ConfigureAwait(false);
        return new PlayerProfileSaveResult(true, "Settings.PlayerNameSaved", normalized);
    }
}

