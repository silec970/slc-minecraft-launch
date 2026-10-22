using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Domain.Abstractions;

public interface IGameRuntime : IAsyncDisposable
{
    event EventHandler<GameExitNotification>? GameExited;

    string RootPath { get; }

    Task<GameRuntimeSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GameVersionDescriptor>> GetVersionsAsync(
        CancellationToken cancellationToken = default);

    Task<ResourceCompletionOutcome> CompleteResourcesAsync(
        ResourceCompletionRequest request,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<ResourceCompletionOutcome> CompleteResourcesObservedAsync(
        ResourceCompletionRequest request,
        IResourceDownloadObserver? observer,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return CompleteResourcesAsync(request, progress, cancellationToken);
    }

    Task<ResourceCompletionOutcome> DownloadModAsync(
        ModDownloadRequest request,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<GameLaunchOutcome> LaunchOfflineAsync(
        GameLaunchRequest request,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<SmokeLaunchOutcome> LaunchSmokeAsync(
        SmokeLaunchRequest request,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

