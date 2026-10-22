using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Domain.Abstractions;

public interface IGameLaunchService
{
    Task<GameLaunchOutcome> LaunchAsync(
        string versionId,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

