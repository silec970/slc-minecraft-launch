using System.Collections.Concurrent;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Wpf.ViewModels;

public abstract partial class PageViewModel : ObservableObject
{
    private readonly ConcurrentDictionary<CancellationTokenSource, byte> _activeOperations = [];

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string _statusKey = "Status.Ready";

    [ObservableProperty]
    private string? _errorDetail;

    public abstract string TitleKey { get; }
    public abstract string DescriptionKey { get; }

    public bool CanCancelCurrentOperation => !_activeOperations.IsEmpty;

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

    protected async Task RunAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        using var linkedCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeOperations.TryAdd(linkedCancellation, 0);
        NotifyCancellationStateChanged();

        IsBusy = true;
        ErrorDetail = null;
        StatusKey = "Status.Working";
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
        }
        catch (Exception exception)
        {
            StatusKey = "Status.Failed";
            ErrorDetail = exception.Message;
        }
        finally
        {
            _activeOperations.TryRemove(linkedCancellation, out _);
            IsBusy = !_activeOperations.IsEmpty;
            NotifyCancellationStateChanged();
        }
    }

    protected void ApplyProgress(OperationProgress operationProgress)
    {
        Progress = operationProgress.Fraction;
        StatusKey = operationProgress.MessageKey;
    }

    protected IProgress<OperationProgress> CreateProgress()
    {
        return new Progress<OperationProgress>(ApplyProgress);
    }

    private void NotifyCancellationStateChanged()
    {
        OnPropertyChanged(nameof(CanCancelCurrentOperation));
        CancelCurrentOperationCommand.NotifyCanExecuteChanged();
    }
}

