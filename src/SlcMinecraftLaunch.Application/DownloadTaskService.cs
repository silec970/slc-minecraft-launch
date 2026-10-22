using System.Collections.Concurrent;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Application;

public sealed class DownloadTaskService(
    IGameRuntimeProvider runtimeProvider,
    IDownloadTaskStore taskStore,
    ISourceHealthService sourceHealthService) : IDownloadTaskService
{
    private readonly SemaphoreSlim _stateLock = new(1, 1);
    private string? _activeTaskId;
    private CancellationTokenSource? _activeCancellation;
    private DownloadTaskRecord? _activeRecord;
    private long _lastProgressNotificationTicks;

    public event EventHandler<DownloadTaskRecord>? TaskChanged;
    public event EventHandler<DownloadTaskFileActivity>? FileCompleted;

    public string? ActiveTaskId => Volatile.Read(ref _activeTaskId);

    public async Task<IReadOnlyList<DownloadTaskRecord>> GetTasksAsync(
        CancellationToken cancellationToken = default)
    {
        var tasks = (await taskStore.LoadAsync(cancellationToken).ConfigureAwait(false))
            .OrderByDescending(task => task.UpdatedAt)
            .ToArray();
        var activeTaskId = ActiveTaskId;
        var recovered = tasks
            .Select(task => task.Status == DownloadTaskStatus.Running &&
                            !string.Equals(task.TaskId, activeTaskId, StringComparison.OrdinalIgnoreCase)
                ? task with
                {
                    Status = DownloadTaskStatus.Paused,
                    MessageKey = "Download.Task.Interrupted",
                    UpdatedAt = DateTimeOffset.UtcNow
                }
                : task)
            .ToArray();
        if (!recovered.SequenceEqual(tasks))
        {
            await taskStore.SaveAsync(recovered, cancellationToken).ConfigureAwait(false);
        }

        return recovered;
    }

    public async Task<DownloadTaskRecord> StartAsync(
        ResourceCompletionRequest request,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var taskId = Guid.NewGuid().ToString("N");
        if (Interlocked.CompareExchange(ref _activeTaskId, taskId, null) is not null)
        {
            throw new InvalidOperationException("Download.Task.AlreadyRunning");
        }

        try
        {
            var runtime = await runtimeProvider.GetAsync(cancellationToken).ConfigureAwait(false);
            var now = DateTimeOffset.UtcNow;
            var record = new DownloadTaskRecord(
                taskId,
                request.VersionId,
                request.JavaPath,
                runtime.RootPath,
                DownloadTaskStatus.Running,
                "Download.Task.Running",
                0,
                1,
                now,
                now,
                [],
                []);
            _activeRecord = record;
            await UpsertAsync(record, cancellationToken).ConfigureAwait(false);
            RaiseTaskChanged(record);

            return await ExecuteAsync(
                runtime,
                record,
                progress,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var failed = (_activeRecord ?? CreateFailedFallback(
                taskId,
                request,
                1)) with
            {
                Status = DownloadTaskStatus.Failed,
                MessageKey = "Download.Task.Failed",
                ErrorDetail = exception.ToString(),
                UpdatedAt = DateTimeOffset.UtcNow
            };
            await UpsertAsync(failed, CancellationToken.None).ConfigureAwait(false);
            RaiseTaskChanged(failed);
            return failed;
        }
        finally
        {
            CompleteActiveTask(taskId);
        }
    }

    public async Task<DownloadTaskRecord?> PauseAsync(
        string taskId,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(ActiveTaskId, taskId, StringComparison.OrdinalIgnoreCase) ||
            _activeRecord is null ||
            _activeRecord.Status != DownloadTaskStatus.Running)
        {
            return null;
        }

        var paused = _activeRecord with
        {
            Status = DownloadTaskStatus.Paused,
            MessageKey = "Download.Task.Paused",
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _activeRecord = paused;
        await UpsertAsync(paused, cancellationToken).ConfigureAwait(false);
        RaiseTaskChanged(paused);
        _activeCancellation?.Cancel();
        return paused;
    }

    public async Task<DownloadTaskRecord> ResumeAsync(
        string taskId,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await ResumeCoreAsync(taskId, progress, cancellationToken).ConfigureAwait(false);
    }

    public async Task<DownloadTaskRecord> RetryFailedAsync(
        string taskId,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return await ResumeCoreAsync(taskId, progress, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> DeleteAsync(
        string taskId,
        CancellationToken cancellationToken = default)
    {
        if (string.Equals(ActiveTaskId, taskId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        await _stateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var tasks = (await taskStore.LoadAsync(cancellationToken).ConfigureAwait(false)).ToList();
            var removed = tasks.RemoveAll(task =>
                string.Equals(task.TaskId, taskId, StringComparison.OrdinalIgnoreCase));
            if (removed == 0)
            {
                return false;
            }

            await taskStore.SaveAsync(tasks, cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            _stateLock.Release();
        }
    }

    private async Task<DownloadTaskRecord> ResumeCoreAsync(
        string taskId,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref _activeTaskId, taskId, null) is not null)
        {
            throw new InvalidOperationException("Download.Task.AlreadyRunning");
        }

        try
        {
            var existing = (await taskStore.LoadAsync(cancellationToken).ConfigureAwait(false))
                .FirstOrDefault(task =>
                    string.Equals(task.TaskId, taskId, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                throw new InvalidOperationException("Download.Task.NotFound");
            }

            if (existing.Status == DownloadTaskStatus.Completed)
            {
                throw new InvalidOperationException("Download.Task.AlreadyCompleted");
            }

            var runtime = await runtimeProvider.GetAsync(cancellationToken).ConfigureAwait(false);
            var running = existing with
            {
                Status = DownloadTaskStatus.Running,
                MessageKey = "Download.Task.Running",
                Attempt = existing.Attempt + 1,
                UpdatedAt = DateTimeOffset.UtcNow,
                ErrorDetail = null
            };
            _activeRecord = running;
            await UpsertAsync(running, cancellationToken).ConfigureAwait(false);
            RaiseTaskChanged(running);
            return await ExecuteAsync(
                runtime,
                running,
                progress,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            var failed = (_activeRecord ?? new DownloadTaskRecord(
                    taskId,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    DownloadTaskStatus.Failed,
                    "Download.Task.Failed",
                    0,
                    1,
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow,
                    [],
                    [])) with
            {
                Status = DownloadTaskStatus.Failed,
                MessageKey = "Download.Task.Failed",
                ErrorDetail = exception.ToString(),
                UpdatedAt = DateTimeOffset.UtcNow
            };
            await UpsertAsync(failed, CancellationToken.None).ConfigureAwait(false);
            RaiseTaskChanged(failed);
            return failed;
        }
        finally
        {
            CompleteActiveTask(taskId);
        }
    }

    private async Task<DownloadTaskRecord> ExecuteAsync(
        IGameRuntime runtime,
        DownloadTaskRecord initial,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeCancellation = linkedCancellation;
        _activeRecord = initial;
        Interlocked.Exchange(ref _lastProgressNotificationTicks, 0);
        var observer = new DownloadObserver(result =>
            FileCompleted?.Invoke(
                this,
                new DownloadTaskFileActivity(
                    initial.TaskId,
                    DateTimeOffset.UtcNow,
                    result)));
        var progressBridge = new ForwardingProgress(value =>
        {
            progress?.Report(value);
            if (_activeRecord is not null)
            {
                _activeRecord = _activeRecord with { Progress = value.Fraction };
                var now = Environment.TickCount64;
                var previous = Volatile.Read(ref _lastProgressNotificationTicks);
                if (now - previous >= 250 &&
                    Interlocked.CompareExchange(
                        ref _lastProgressNotificationTicks,
                        now,
                        previous) == previous)
                {
                    RaiseTaskChanged(_activeRecord);
                }
            }
        });

        try
        {
            var outcome = await runtime.CompleteResourcesObservedAsync(
                new ResourceCompletionRequest(initial.VersionId, initial.JavaPath),
                observer,
                progressBridge,
                linkedCancellation.Token).ConfigureAwait(false);
            await RecordSourceHealthAsync(observer, cancellationToken).ConfigureAwait(false);

            var final = initial with
            {
                Status = outcome.Succeeded
                    ? DownloadTaskStatus.Completed
                    : DownloadTaskStatus.Failed,
                MessageKey = outcome.MessageKey,
                Progress = outcome.Succeeded ? 1 : _activeRecord?.Progress ?? initial.Progress,
                ErrorDetail = outcome.ErrorDetail,
                FailedFiles = outcome.Succeeded
                    ? []
                    : observer.GetFailedFiles(),
                Sources = observer.GetSources(),
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _activeRecord = final;
            await UpsertAsync(final, CancellationToken.None).ConfigureAwait(false);
            RaiseTaskChanged(final);
            return final;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await RecordSourceHealthAsync(observer, CancellationToken.None).ConfigureAwait(false);
            await MarkPausedAsync(initial, observer, CancellationToken.None).ConfigureAwait(false);
            throw new OperationCanceledException(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            await RecordSourceHealthAsync(observer, CancellationToken.None).ConfigureAwait(false);
            return await MarkPausedAsync(initial, observer, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await RecordSourceHealthAsync(observer, CancellationToken.None).ConfigureAwait(false);
            var failed = initial with
            {
                Status = DownloadTaskStatus.Failed,
                MessageKey = "Download.Task.Failed",
                ErrorDetail = exception.ToString(),
                FailedFiles = observer.GetFailedFiles(),
                Sources = observer.GetSources(),
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _activeRecord = failed;
            await UpsertAsync(failed, CancellationToken.None).ConfigureAwait(false);
            RaiseTaskChanged(failed);
            return failed;
        }
        finally
        {
            _activeCancellation = null;
        }
    }

    private async Task<DownloadTaskRecord> MarkPausedAsync(
        DownloadTaskRecord initial,
        DownloadObserver observer,
        CancellationToken cancellationToken)
    {
        var paused = initial with
        {
            Status = DownloadTaskStatus.Paused,
            MessageKey = "Download.Task.Paused",
            Progress = _activeRecord?.Progress ?? initial.Progress,
            FailedFiles = observer.GetFailedFiles(),
            Sources = observer.GetSources(),
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _activeRecord = paused;
        await UpsertAsync(paused, cancellationToken).ConfigureAwait(false);
        RaiseTaskChanged(paused);
        return paused;
    }

    private async Task RecordSourceHealthAsync(
        DownloadObserver observer,
        CancellationToken cancellationToken)
    {
        var observations = observer.GetSourceObservations();
        if (observations.Count == 0)
        {
            return;
        }

        try
        {
            await sourceHealthService
                .RecordAsync(observations, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Source health is advisory and must not change the download result.
        }
    }

    private async Task UpsertAsync(
        DownloadTaskRecord record,
        CancellationToken cancellationToken)
    {
        await _stateLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var tasks = (await taskStore.LoadAsync(cancellationToken).ConfigureAwait(false)).ToList();
            var index = tasks.FindIndex(task =>
                string.Equals(task.TaskId, record.TaskId, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                tasks[index] = record;
            }
            else
            {
                tasks.Add(record);
            }

            if (tasks.Count > 100)
            {
                tasks = tasks
                    .OrderByDescending(task => task.UpdatedAt)
                    .Take(100)
                    .ToList();
            }

            await taskStore.SaveAsync(tasks, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _stateLock.Release();
        }
    }

    private void CompleteActiveTask(string taskId)
    {
        Interlocked.CompareExchange(ref _activeTaskId, null, taskId);
        _activeRecord = null;
        _activeCancellation?.Dispose();
        _activeCancellation = null;
    }

    private void RaiseTaskChanged(DownloadTaskRecord record)
    {
        TaskChanged?.Invoke(this, record);
    }

    private static DownloadTaskRecord CreateFailedFallback(
        string taskId,
        ResourceCompletionRequest request,
        int attempt)
    {
        var now = DateTimeOffset.UtcNow;
        return new DownloadTaskRecord(
            taskId,
            request.VersionId,
            request.JavaPath,
            string.Empty,
            DownloadTaskStatus.Failed,
            "Download.Task.Failed",
            0,
            attempt,
            now,
            now,
            [],
            []);
    }

    private sealed class ForwardingProgress(Action<OperationProgress> action)
        : IProgress<OperationProgress>
    {
        public void Report(OperationProgress value) => action(value);
    }

    private sealed class DownloadObserver(Action<ResourceDownloadFileResult> onFileCompleted)
        : IResourceDownloadObserver
    {
        private readonly ConcurrentDictionary<string, DownloadTaskFileRecord> _failedFiles =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, SourceCounter> _sources =
            new(StringComparer.OrdinalIgnoreCase);

        public void OnFileCompleted(ResourceDownloadFileResult result)
        {
            if (!string.IsNullOrWhiteSpace(result.SourceRoot))
            {
                var counter = _sources.GetOrAdd(result.SourceRoot, _ => new SourceCounter());
                if (result.Succeeded)
                {
                    Interlocked.Increment(ref counter.Successes);
                }
                else
                {
                    Interlocked.Increment(ref counter.Failures);
                }
            }

            onFileCompleted(result);

            if (result.Succeeded)
            {
                return;
            }

            var key = $"{result.TargetPath}\0{result.FileName}";
            _failedFiles[key] = new DownloadTaskFileRecord(
                result.FileName,
                result.TargetPath,
                result.SourceUrl,
                result.FileSize,
                result.CheckSum,
                result.ErrorDetail);
        }

        public IReadOnlyList<DownloadTaskFileRecord> GetFailedFiles()
        {
            return _failedFiles.Values
                .OrderBy(file => file.TargetPath, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public IReadOnlyList<string> GetSources()
        {
            return _sources.Keys
                .OrderBy(source => source, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public IReadOnlyList<DownloadSourceObservation> GetSourceObservations()
        {
            return _sources.Select(pair => new DownloadSourceObservation(
                    pair.Key,
                    Volatile.Read(ref pair.Value.Successes),
                    Volatile.Read(ref pair.Value.Failures)))
                .Where(observation => observation.SuccessCount > 0 || observation.FailureCount > 0)
                .ToArray();
        }

        private sealed class SourceCounter
        {
            public int Successes;
            public int Failures;
        }
    }
}
