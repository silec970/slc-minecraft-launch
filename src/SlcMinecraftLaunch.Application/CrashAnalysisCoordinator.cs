using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Application;

public sealed class CrashAnalysisCoordinator(
    ICrashAnalysisService crashAnalysisService,
    IAppLogger appLogger) : IAsyncDisposable
{
    private readonly SemaphoreSlim _analysisGate = new(1, 1);
    private readonly CancellationTokenSource _shutdown = new();
    private IGameRuntime? _runtime;
    private Task? _backgroundAnalysis;

    public string? LastError { get; private set; }

    public void Start(IGameRuntime runtime)
    {
        if (_runtime is not null)
        {
            return;
        }

        _runtime = runtime;
        _runtime.GameExited += OnGameExited;
    }

    public void Stop()
    {
        if (_runtime is not null)
        {
            _runtime.GameExited -= OnGameExited;
            _runtime = null;
        }
    }

    private void OnGameExited(object? sender, GameExitNotification notification)
    {
        if (!_analysisGate.Wait(0))
        {
            return;
        }

        _backgroundAnalysis = AnalyzeAfterSettleAsync(notification);
    }

    private async Task AnalyzeAfterSettleAsync(GameExitNotification notification)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(750), _shutdown.Token).ConfigureAwait(false);
            await crashAnalysisService.AnalyzeAsync(
                notification.VersionId,
                notification.ExitCode,
                _shutdown.Token).ConfigureAwait(false);
            LastError = null;
        }
        catch (OperationCanceledException) when (_shutdown.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            LastError = exception.ToString();
            appLogger.Error(
                $"Background crash analysis failed for '{notification.VersionId}'.",
                exception);
        }
        finally
        {
            _analysisGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        Stop();
        _shutdown.Cancel();
        if (_backgroundAnalysis is not null)
        {
            await _backgroundAnalysis.ConfigureAwait(false);
        }

        _shutdown.Dispose();
        _analysisGate.Dispose();
    }
}
