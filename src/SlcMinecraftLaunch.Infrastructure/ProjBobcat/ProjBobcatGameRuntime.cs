using ProjBobcat.Event;
using ProjBobcat.Interface;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;
using SlcMinecraftLaunch.Infrastructure.Configuration;

namespace SlcMinecraftLaunch.Infrastructure.ProjBobcat;

public sealed class ProjBobcatGameRuntime : IGameRuntime
{
    private readonly ProjBobcatCoreComponents _components;
    private readonly VersionCatalogAdapter _versionCatalogAdapter;
    private readonly ResourceDownloadAdapter _resourceDownloadAdapter;
    private readonly LaunchAdapter _launchAdapter;
    private readonly ModDownloadAdapter _modDownloadAdapter;
    private bool _disposed;

    private ProjBobcatGameRuntime(
        ProjBobcatCoreComponents components,
        IHttpClientFactory httpClientFactory,
        DownloadSourceOptions downloadSourceOptions,
        ISourceHealthService sourceHealthService)
    {
        _components = components;
        _versionCatalogAdapter = new VersionCatalogAdapter(components.VersionLocator);
        _resourceDownloadAdapter = new ResourceDownloadAdapter(
            components.VersionLocator,
            httpClientFactory,
            downloadSourceOptions,
            sourceHealthService,
            components.RootPath);
        _launchAdapter = new LaunchAdapter(
            components.GameCore,
            components.VersionLocator,
            components.AccountParser,
            _resourceDownloadAdapter,
            components.RootPath);
        _modDownloadAdapter = new ModDownloadAdapter(
            components.VersionLocator,
            httpClientFactory,
            components.RootPath);
        // PROJBOBCAT: GameExitEventDelegate is the public process lifecycle
        // contract consumed by the crash-analysis hook.
        components.GameCore.GameExitEventDelegate += ForwardGameExit;
    }

    public event EventHandler<GameExitNotification>? GameExited;

    public string RootPath => _components.RootPath;

    internal IVersionLocator VersionLocator => _components.VersionLocator;

    public static async Task<ProjBobcatGameRuntime> CreateAsync(
        PortableLauncherPaths paths,
        IHttpClientFactory httpClientFactory,
        DownloadSourceOptions downloadSourceOptions,
        ISourceHealthService sourceHealthService,
        CancellationToken cancellationToken = default)
    {
        var components = await ProjBobcatCoreFactory
            .CreateAsync(paths, cancellationToken)
            .ConfigureAwait(false);
        return new ProjBobcatGameRuntime(
            components,
            httpClientFactory,
            downloadSourceOptions,
            sourceHealthService);
    }

    public Task<GameRuntimeSnapshot> GetSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        return _versionCatalogAdapter.GetSnapshotAsync(RootPath, cancellationToken);
    }

    public Task<IReadOnlyList<GameVersionDescriptor>> GetVersionsAsync(
        CancellationToken cancellationToken = default)
    {
        return _versionCatalogAdapter.GetVersionsAsync(cancellationToken);
    }

    public Task<ResourceCompletionOutcome> CompleteResourcesAsync(
        ResourceCompletionRequest request,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return _resourceDownloadAdapter.CompleteResourcesAsync(
            request,
            null,
            progress,
            cancellationToken);
    }

    public Task<ResourceCompletionOutcome> CompleteResourcesObservedAsync(
        ResourceCompletionRequest request,
        IResourceDownloadObserver? observer,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return _resourceDownloadAdapter.CompleteResourcesAsync(
            request,
            observer,
            progress,
            cancellationToken);
    }

    public Task<SmokeLaunchOutcome> LaunchSmokeAsync(
        SmokeLaunchRequest request,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return _launchAdapter.LaunchSmokeAsync(request, progress, cancellationToken);
    }

    public Task<GameLaunchOutcome> LaunchOfflineAsync(
        GameLaunchRequest request,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return _launchAdapter.LaunchOfflineAsync(request, progress, cancellationToken);
    }

    public Task<ResourceCompletionOutcome> DownloadModAsync(
        ModDownloadRequest request,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return _modDownloadAdapter.DownloadModAsync(request, progress, cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }

        _disposed = true;
        _components.GameCore.GameExitEventDelegate -= ForwardGameExit;
        _components.GameCore.Dispose();
        return ValueTask.CompletedTask;
    }

    private void ForwardGameExit(object? sender, GameExitEventArgs eventArgs)
    {
        if (_disposed)
        {
            return;
        }

        GameExited?.Invoke(this, new GameExitNotification(
            eventArgs.SourceGameId,
            eventArgs.ExitCode,
            DateTimeOffset.Now,
            eventArgs.Exception?.ToString()));
    }
}
