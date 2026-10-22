namespace SlcMinecraftLaunch.Domain.Models;

public sealed record CurseForgeCredentialStatus(
    bool IsConfigured,
    string MessageKey);
