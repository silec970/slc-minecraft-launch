using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Application;

public sealed class DownloadService(IGameRuntimeProvider runtimeProvider) : IDownloadService
{
    public async Task<ResourceCompletionOutcome> CompleteResourcesAsync(
        ResourceCompletionRequest request,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var runtime = await runtimeProvider.GetAsync(cancellationToken).ConfigureAwait(false);
        return await runtime.CompleteResourcesAsync(request, progress, cancellationToken)
            .ConfigureAwait(false);
    }
}
