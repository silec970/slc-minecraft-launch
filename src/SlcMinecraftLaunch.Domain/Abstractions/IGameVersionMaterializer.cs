using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Domain.Abstractions;

public interface IGameVersionMaterializer
{
    Task EnsureVersionAsync(
        string rootPath,
        string targetVersion,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

