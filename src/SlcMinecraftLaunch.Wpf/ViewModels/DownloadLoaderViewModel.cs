using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Wpf.ViewModels;

public sealed partial class DownloadLoaderViewModel : PageViewModel
{
    private readonly ILoaderCatalogService _loaderCatalog;
    private readonly ILoaderInstallationService _loaderInstallation;
    private readonly IJavaRuntimeService _javaRuntimeService;
    private readonly IVanillaVersionCatalogService _vanillaVersionCatalog;
    private readonly IVanillaInstallationService _vanillaInstallation;
    private readonly IDownloadTaskService _downloadTaskService;
    private readonly IInstanceService _instanceService;
    private IReadOnlyList<MinecraftReleaseDescriptor> _allCatalogVersions =
        Array.Empty<MinecraftReleaseDescriptor>();
    private string? _lastStageMessageKey;

    public DownloadLoaderViewModel(
        ILoaderCatalogService loaderCatalog,
        ILoaderInstallationService loaderInstallation,
        IJavaRuntimeService javaRuntimeService,
        IVanillaVersionCatalogService vanillaVersionCatalog,
        IVanillaInstallationService vanillaInstallation,
        IDownloadTaskService downloadTaskService,
        IInstanceService instanceService)
    {
        ArgumentNullException.ThrowIfNull(downloadTaskService);
        _loaderCatalog = loaderCatalog;
        _loaderInstallation = loaderInstallation;
        _javaRuntimeService = javaRuntimeService;
        _vanillaVersionCatalog = vanillaVersionCatalog;
        _vanillaInstallation = vanillaInstallation;
        _downloadTaskService = downloadTaskService;
        _instanceService = instanceService;
        _downloadTaskService.TaskChanged += OnTaskChanged;
        _downloadTaskService.FileCompleted += OnFileCompleted;
    }

    public override string TitleKey => "Download.Title";
    public override string DescriptionKey => "Download.Description";

    public ObservableCollection<MinecraftReleaseDescriptor> CatalogVersions { get; } = [];
    public ObservableCollection<LoaderVersionOption> LoaderVersions { get; } = [];
    public ObservableCollection<DownloadTaskRecord> DownloadTasks { get; } = [];
    public ObservableCollection<DownloadActivityRow> DownloadActivity { get; } = [];
    public IReadOnlyList<LoaderKind> LoaderKinds { get; } =
        [LoaderKind.Fabric, LoaderKind.Forge, LoaderKind.NeoForge, LoaderKind.OptiFine, LoaderKind.LiteLoader];

    [ObservableProperty]
    private string? _selectedMinecraftVersion;

    [ObservableProperty]
    private MinecraftReleaseDescriptor? _selectedCatalogVersion;

    [ObservableProperty]
    private bool _showSnapshots;

    [ObservableProperty]
    private LoaderKind _selectedLoaderKind = LoaderKind.Fabric;

    [ObservableProperty]
    private LoaderVersionOption? _selectedLoaderVersion;

    [ObservableProperty]
    private string? _javaPath;

    [ObservableProperty]
    private string? _customVersionId;

    [ObservableProperty]
    private string? _installedVersionId;

    [ObservableProperty]
    private bool _canCompleteResources;

    [ObservableProperty]
    private DownloadTaskRecord? _selectedDownloadTask;

    [ObservableProperty]
    private string? _activeDownloadTaskId;

    public bool CanPauseDownload => !string.IsNullOrWhiteSpace(ActiveDownloadTaskId);
    public bool CanRetryDownload => SelectedDownloadTask?.CanRetry == true;
    public bool CanDeleteDownload => SelectedDownloadTask?.CanDelete == true;
    public bool CanCompleteDownload =>
        CanCompleteResources && string.IsNullOrWhiteSpace(ActiveDownloadTaskId);
    public bool HasDownloadActivity => DownloadActivity.Count > 0;

    partial void OnShowSnapshotsChanged(bool value)
    {
        ApplyCatalogFilter();
    }

    partial void OnSelectedCatalogVersionChanged(MinecraftReleaseDescriptor? value)
    {
        SelectedMinecraftVersion = value?.Id;
    }

    partial void OnSelectedDownloadTaskChanged(DownloadTaskRecord? value)
    {
        OnPropertyChanged(nameof(CanRetryDownload));
        OnPropertyChanged(nameof(CanDeleteDownload));
    }

    partial void OnActiveDownloadTaskIdChanged(string? value)
    {
        OnPropertyChanged(nameof(CanPauseDownload));
        OnPropertyChanged(nameof(CanCompleteDownload));
    }

    partial void OnCanCompleteResourcesChanged(bool value)
    {
        OnPropertyChanged(nameof(CanCompleteDownload));
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private Task LoadAsync(CancellationToken cancellationToken)
    {
        return RunAsync(async token =>
        {
            BeginDownloadActivity();
            _allCatalogVersions = await _vanillaVersionCatalog
                .GetVersionsAsync(token)
                .ConfigureAwait(true);
            ApplyCatalogFilter();
            await ReloadTasksAsync(token);
        }, cancellationToken);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private Task InstallVanillaAsync(CancellationToken cancellationToken)
    {
        return RunAsync(async token =>
        {
            if (SelectedCatalogVersion is null)
            {
                throw new UiOperationException("Download.SelectVersionRequired");
            }

            BeginDownloadActivity();
            var result = await _vanillaInstallation.InstallAsync(
                SelectedCatalogVersion.Id,
                CreateProgress(),
                token);
            InstalledVersionId = result.VersionId;
            StatusKey = result.MessageKey;
            ErrorDetail = result.ErrorDetail;
            ShowOperationResult(
                result.Succeeded,
                result.MessageKey,
                result.ErrorDetail,
                result.VersionId);
        }, cancellationToken);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private Task ResolveLoaderVersionsAsync(CancellationToken cancellationToken)
    {
        return RunAsync(async token =>
        {
            if (string.IsNullOrWhiteSpace(SelectedMinecraftVersion))
            {
                throw new UiOperationException("Download.SelectVersionRequired");
            }

            if (SelectedLoaderKind == LoaderKind.LiteLoader)
            {
                LoaderVersions.Clear();
                SelectedLoaderVersion = null;
                StatusKey = "Download.LiteLoaderUnsupported";
                return;
            }

            var versions = await _loaderCatalog.GetVersionsAsync(
                SelectedMinecraftVersion,
                SelectedLoaderKind,
                token);
            LoaderVersions.Clear();
            foreach (var version in versions)
            {
                LoaderVersions.Add(version);
            }

            SelectedLoaderVersion = LoaderVersions.FirstOrDefault(version => version.IsStable)
                                    ?? LoaderVersions.FirstOrDefault();
        }, cancellationToken);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private Task InstallLoaderAsync(CancellationToken cancellationToken)
    {
        return RunAsync(async token =>
        {
            if (string.IsNullOrWhiteSpace(SelectedMinecraftVersion))
            {
                throw new UiOperationException("Download.SelectVersionRequired");
            }

            BeginDownloadActivity();
            var result = await _loaderInstallation.InstallAsync(
                new LoaderInstallRequest(
                    SelectedMinecraftVersion,
                    SelectedLoaderKind,
                    SelectedLoaderVersion?.Version,
                    JavaPath,
                    string.IsNullOrWhiteSpace(CustomVersionId) ? null : CustomVersionId.Trim()),
                CreateProgress(),
                token);
            InstalledVersionId = result.InstalledVersionId;
            CanCompleteResources = result.Succeeded;
            StatusKey = result.MessageKey;
            ErrorDetail = result.ErrorDetail;
            ShowOperationResult(
                result.Succeeded,
                result.MessageKey,
                result.ErrorDetail,
                result.InstalledVersionId);
            if (result.Succeeded && !string.IsNullOrWhiteSpace(result.InstalledVersionId))
            {
                await MarkInstanceReadyAsync(result.InstalledVersionId, token);
            }
        }, cancellationToken);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private Task CompleteResourcesAsync(CancellationToken cancellationToken)
    {
        return RunAsync(async token =>
        {
            if (string.IsNullOrWhiteSpace(InstalledVersionId))
            {
                throw new UiOperationException("Download.InstallLoaderRequired");
            }

            BeginDownloadActivity();
            var java = await _javaRuntimeService.EnsureAsync(
                InstalledVersionId,
                SelectedLoaderKind,
                JavaPath,
                CreateProgress(),
                token);
            if (java.Selected is null)
            {
                throw new UiOperationException(java.MessageKey);
            }

            var task = await _downloadTaskService.StartAsync(
                new ResourceCompletionRequest(InstalledVersionId, java.Selected.ExecutablePath),
                CreateProgress(),
                token);
            ApplyTask(task);
            StatusKey = task.MessageKey;
            ErrorDetail = task.ErrorDetail;
            ShowTaskResult(task);
        }, cancellationToken);
    }

    [RelayCommand]
    private Task PauseDownloadAsync(CancellationToken cancellationToken)
    {
        return RunAsync(async token =>
        {
            if (string.IsNullOrWhiteSpace(ActiveDownloadTaskId))
            {
                throw new UiOperationException("Download.Task.NotRunning");
            }

            var task = await _downloadTaskService.PauseAsync(
                ActiveDownloadTaskId,
                token);
            if (task is null)
            {
                throw new UiOperationException("Download.Task.NotRunning");
            }

            ApplyTask(task);
            StatusKey = task.MessageKey;
        }, cancellationToken);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private Task ResumeDownloadAsync(string? taskId, CancellationToken cancellationToken)
    {
        return RunTaskOperationAsync(
            taskId,
            _downloadTaskService.ResumeAsync,
            cancellationToken);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private Task RetryFailedAsync(string? taskId, CancellationToken cancellationToken)
    {
        return RunTaskOperationAsync(
            taskId,
            _downloadTaskService.RetryFailedAsync,
            cancellationToken);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private Task DeleteDownloadTaskAsync(string? taskId, CancellationToken cancellationToken)
    {
        return RunAsync(async token =>
        {
            if (string.IsNullOrWhiteSpace(taskId) ||
                !await _downloadTaskService.DeleteAsync(taskId, token))
            {
                throw new UiOperationException("Download.Task.NotFound");
            }

            var existing = DownloadTasks.FirstOrDefault(task =>
                string.Equals(task.TaskId, taskId, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                DownloadTasks.Remove(existing);
            }

            SelectedDownloadTask = DownloadTasks.FirstOrDefault();
            StatusKey = "Download.Task.Deleted";
        }, cancellationToken);
    }

    public override Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        return LoadAsync(cancellationToken);
    }

    private Task RunTaskOperationAsync(
        string? taskId,
        Func<string, IProgress<OperationProgress>?, CancellationToken, Task<DownloadTaskRecord>> operation,
        CancellationToken cancellationToken)
    {
        return RunAsync(async token =>
        {
            if (string.IsNullOrWhiteSpace(taskId))
            {
                throw new UiOperationException("Download.Task.NotFound");
            }

            BeginDownloadActivity();
            var task = await operation(taskId, CreateProgress(), token);
            ApplyTask(task);
            StatusKey = task.MessageKey;
            ErrorDetail = task.ErrorDetail;
            ShowTaskResult(task);
        }, cancellationToken);
    }

    protected override void ApplyProgress(OperationProgress operationProgress)
    {
        base.ApplyProgress(operationProgress);
        if (string.Equals(
                _lastStageMessageKey,
                operationProgress.MessageKey,
                StringComparison.Ordinal))
        {
            return;
        }

        _lastStageMessageKey = operationProgress.MessageKey;
        AppendDownloadActivity(new DownloadActivityRow(
            DateTimeOffset.Now,
            null,
            operationProgress.MessageKey,
            "Download.LiveLog.Stage",
            string.Empty,
            operationProgress.Detail));
    }

    private async Task ReloadTasksAsync(CancellationToken cancellationToken)
    {
        var tasks = await _downloadTaskService.GetTasksAsync(cancellationToken);
        DownloadTasks.Clear();
        foreach (var task in tasks)
        {
            DownloadTasks.Add(task);
        }

        ActiveDownloadTaskId = _downloadTaskService.ActiveTaskId;
        SelectedDownloadTask = DownloadTasks.FirstOrDefault();
    }

    private void OnTaskChanged(object? sender, DownloadTaskRecord task)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(() => ApplyTask(task));
            return;
        }

        ApplyTask(task);
    }

    private void OnFileCompleted(object? sender, DownloadTaskFileActivity activity)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(() => AppendFileActivity(activity));
            return;
        }

        AppendFileActivity(activity);
    }

    private void ApplyTask(DownloadTaskRecord task)
    {
        var existing = DownloadTasks.FirstOrDefault(item =>
            string.Equals(item.TaskId, task.TaskId, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            DownloadTasks.Remove(existing);
        }

        DownloadTasks.Insert(0, task);
        SelectedDownloadTask = task;
        ActiveDownloadTaskId = task.Status == DownloadTaskStatus.Running
            ? task.TaskId
            : null;
    }

    private void ApplyCatalogFilter()
    {
        var selectedId = SelectedCatalogVersion?.Id;
        CatalogVersions.Clear();
        foreach (var version in _allCatalogVersions.Where(version =>
                     version.Type == "release" || ShowSnapshots))
        {
            CatalogVersions.Add(version);
        }

        SelectedCatalogVersion = CatalogVersions.FirstOrDefault(version =>
                                    string.Equals(version.Id, selectedId, StringComparison.OrdinalIgnoreCase))
                                ?? CatalogVersions.FirstOrDefault(version => version.IsLatestRelease)
                                ?? CatalogVersions.FirstOrDefault();
    }

    private void BeginDownloadActivity()
    {
        DownloadActivity.Clear();
        _lastStageMessageKey = null;
        OnPropertyChanged(nameof(HasDownloadActivity));
    }

    private void AppendFileActivity(DownloadTaskFileActivity activity)
    {
        var file = activity.File;
        AppendDownloadActivity(new DownloadActivityRow(
            activity.CompletedAt.ToLocalTime(),
            file.FileName,
            null,
            file.Succeeded ? "Download.LiveLog.FileDownloaded" : "Download.LiveLog.FileFailed",
            FormatFileSize(file.FileSize),
            BuildFileDetail(file)));
    }

    private void AppendDownloadActivity(DownloadActivityRow activity)
    {
        DownloadActivity.Add(activity);
        OnPropertyChanged(nameof(HasDownloadActivity));
    }

    private void ShowTaskResult(DownloadTaskRecord task)
    {
        if (task.Status == DownloadTaskStatus.Completed)
        {
            ShowSuccessNotification(task.MessageKey, task.VersionId);
        }
        else if (task.Status == DownloadTaskStatus.Failed)
        {
            ShowFailureNotification(task.MessageKey, task.ErrorDetail);
        }
    }

    private void ShowOperationResult(
        bool succeeded,
        string messageKey,
        string? errorDetail,
        string? detail)
    {
        if (succeeded)
        {
            ShowSuccessNotification(messageKey, detail);
        }
        else
        {
            ShowFailureNotification(messageKey, errorDetail ?? detail);
        }
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes <= 0)
        {
            return string.Empty;
        }

        string[] units = ["B", "KB", "MB", "GB"];
        var size = (double)bytes;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return $"{size:0.#} {units[unit]}";
    }

    private static string? BuildFileDetail(ResourceDownloadFileResult file)
    {
        var details = new[]
        {
            file.TargetPath,
            file.SourceUrl,
            file.CheckSum,
            file.ErrorDetail
        };
        var populated = details
            .Where(detail => !string.IsNullOrWhiteSpace(detail))
            .ToArray();
        return populated.Length == 0 ? null : string.Join(Environment.NewLine, populated);
    }

    private async Task MarkInstanceReadyAsync(
        string installedVersionId,
        CancellationToken cancellationToken)
    {
        var manifest = await _instanceService
            .GetManifestAsync(installedVersionId, cancellationToken);
        if (manifest is null)
        {
            return;
        }

        var installed = (await _instanceService.GetInstancesAsync(cancellationToken))
            .FirstOrDefault(instance =>
                string.Equals(instance.Id, installedVersionId, StringComparison.OrdinalIgnoreCase));
        if (installed is null)
        {
            return;
        }

        await _instanceService.SaveAsync(
            manifest with
            {
                MinecraftVersion = installed.MinecraftVersion,
                LoaderKind = installed.LoaderKind,
                LoaderVersion = installed.LoaderVersion,
                IsPlaceholder = false
            },
            cancellationToken);
    }
}

public sealed record DownloadActivityRow(
    DateTimeOffset Timestamp,
    string? FileName,
    string? StageMessageKey,
    string StatusKey,
    string FileSize,
    string? Detail)
{
    public bool IsStage => StageMessageKey is not null;
}
