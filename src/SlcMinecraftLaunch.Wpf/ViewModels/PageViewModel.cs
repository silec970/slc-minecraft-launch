using System.Collections.Concurrent;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Wpf.ViewModels;

public abstract partial class PageViewModel : ObservableObject
{
    private const string OverlayCharacter = "\u00D7";

    private readonly ConcurrentDictionary<CancellationTokenSource, byte> _activeOperations = [];
    private CancellationTokenSource? _notificationCancellation;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string _statusKey = "Status.Ready";

    [ObservableProperty]
    private string? _errorDetail;

    [ObservableProperty]
    private bool _isNotificationVisible;

    [ObservableProperty]
    private bool _isNotificationError;

    [ObservableProperty]
    private string _notificationTitleKey = "Notification.SuccessTitle";

    [ObservableProperty]
    private string _notificationMessageKey = "Status.Ready";

    [ObservableProperty]
    private string? _notificationDetail;

    public abstract string TitleKey { get; }
    public abstract string DescriptionKey { get; }

    public bool CanCancelCurrentOperation => !_activeOperations.IsEmpty;
    public string DismissGlyph => OverlayCharacter;

    protected virtual TimeSpan NotificationDisplayDuration => TimeSpan.FromSeconds(6);

    public abstract Task RefreshAsync(CancellationToken cancellationToken = default);

    [RelayCommand(CanExecute = nameof(CanCancelCurrentOperation))]
    private void CancelCurrentOperation()
    {
        foreach (var operation in _activeOperations.Keys)
        {
            try
            {
                operation.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The operation completed before the user's click reached it.
            }
        }
    }

    [RelayCommand]
    private void DismissNotification()
    {
        HideNotification();
    }

    protected async Task RunAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        using var linkedCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeOperations.TryAdd(linkedCancellation, 0);
        NotifyCancellationStateChanged();

        IsBusy = true;
        Progress = 0;
        ErrorDetail = null;
        StatusKey = "Status.Working";
        HideNotification();
        try
        {
            await action(linkedCancellation.Token).ConfigureAwait(true);
            if (StatusKey == "Status.Working")
            {
                StatusKey = "Status.Ready";
            }
        }
        catch (OperationCanceledException) when (linkedCancellation.IsCancellationRequested)
        {
            StatusKey = "Status.Cancelled";
        }
        catch (UiOperationException exception)
        {
            StatusKey = exception.MessageKey;
            ShowFailureNotification(exception.MessageKey);
        }
        catch (Exception exception)
        {
            StatusKey = "Status.Failed";
            ErrorDetail = exception.Message;
            ShowFailureNotification("Status.Failed", exception.Message);
        }
        finally
        {
            _activeOperations.TryRemove(linkedCancellation, out _);
            IsBusy = !_activeOperations.IsEmpty;
            NotifyCancellationStateChanged();
        }
    }

    protected virtual void ApplyProgress(OperationProgress operationProgress)
    {
        Progress = operationProgress.Fraction;
        StatusKey = operationProgress.MessageKey;
    }

    protected IProgress<OperationProgress> CreateProgress()
    {
        return new Progress<OperationProgress>(ApplyProgress);
    }

    protected void ShowSuccessNotification(string messageKey, string? detail = null)
    {
        ShowNotification(false, "Notification.SuccessTitle", messageKey, detail);
    }

    protected void ShowFailureNotification(string messageKey, string? detail = null)
    {
        ShowNotification(true, "Notification.FailureTitle", messageKey, detail);
    }

    private void ShowNotification(
        bool isError,
        string titleKey,
        string messageKey,
        string? detail)
    {
        _notificationCancellation?.Cancel();
        _notificationCancellation?.Dispose();

        IsNotificationError = isError;
        NotificationTitleKey = titleKey;
        NotificationMessageKey = messageKey;
        NotificationDetail = string.IsNullOrWhiteSpace(detail) ? null : detail;
        IsNotificationVisible = true;

        var cancellation = new CancellationTokenSource();
        _notificationCancellation = cancellation;
        _ = HideNotificationAfterDelayAsync(cancellation);
    }

    private async Task HideNotificationAfterDelayAsync(CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(NotificationDisplayDuration, cancellation.Token).ConfigureAwait(true);
            if (!cancellation.IsCancellationRequested)
            {
                HideNotification();
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // Replaced by a newer notification or dismissed by the user.
        }
    }

    private void HideNotification()
    {
        _notificationCancellation?.Cancel();
        _notificationCancellation?.Dispose();
        _notificationCancellation = null;
        IsNotificationVisible = false;
    }

    private void NotifyCancellationStateChanged()
    {
        OnPropertyChanged(nameof(CanCancelCurrentOperation));
        CancelCurrentOperationCommand.NotifyCanExecuteChanged();
    }
}

