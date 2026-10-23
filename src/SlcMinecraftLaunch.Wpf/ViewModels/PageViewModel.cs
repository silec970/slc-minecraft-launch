using CommunityToolkit.Mvvm.ComponentModel;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Wpf.ViewModels;

public abstract partial class PageViewModel : ObservableObject
{
    private int _activeOperationCount;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string _statusKey = "Status.Ready";

    [ObservableProperty]
    private string? _errorDetail;

    public abstract string TitleKey { get; }
    public abstract string DescriptionKey { get; }
    public bool IsBusy => Volatile.Read(ref _activeOperationCount) > 0;

    public abstract Task RefreshAsync(CancellationToken cancellationToken = default);

    protected async Task RunAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _activeOperationCount);
        OnPropertyChanged(nameof(IsBusy));
        ErrorDetail = null;
        StatusKey = "Status.Working";
        try
        {
            await action(cancellationToken).ConfigureAwait(true);
            if (StatusKey == "Status.Working")
            {
                StatusKey = "Status.Ready";
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
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
            if (Interlocked.Decrement(ref _activeOperationCount) == 0)
            {
                OnPropertyChanged(nameof(IsBusy));
            }
        }
    }

    internal void ReportActivationFailure(Exception exception)
    {
        StatusKey = "Status.Failed";
        ErrorDetail = exception.Message;
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
}

