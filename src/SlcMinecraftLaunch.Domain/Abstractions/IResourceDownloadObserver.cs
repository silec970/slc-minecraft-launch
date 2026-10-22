using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Domain.Abstractions;

public interface IResourceDownloadObserver
{
    void OnFileCompleted(ResourceDownloadFileResult result);
}
