using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Domain.Abstractions;

public interface ICurseForgeCredentialService
{
    bool IsConfigured { get; }

    Task<CurseForgeCredentialStatus> LoadStatusAsync(
        CancellationToken cancellationToken = default);

    Task<CurseForgeCredentialStatus> SaveApiKeyAsync(
        string apiKey,
        CancellationToken cancellationToken = default);

    Task<CurseForgeCredentialStatus> ClearApiKeyAsync(
        CancellationToken cancellationToken = default);
}
