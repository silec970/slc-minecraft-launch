namespace SlcMinecraftLaunch.Domain.Models;

public sealed record PlayerProfileSettings(string PlayerName = "");

public sealed record PlayerProfileSaveResult(
    bool Succeeded,
    string MessageKey,
    string PlayerName);

