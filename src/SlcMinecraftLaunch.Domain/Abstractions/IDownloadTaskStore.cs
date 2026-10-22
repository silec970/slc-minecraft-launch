using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Domain.Abstractions;

public interface IDownloadTaskStore
{
    Task<IReadOnlyList<DownloadTaskRecord>> LoadAsync(
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        IReadOnlyList<DownloadTaskRecord> tasks,
        CancellationToken cancellationToken = default);
}
