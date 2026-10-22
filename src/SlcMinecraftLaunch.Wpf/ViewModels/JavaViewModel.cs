using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Wpf.ViewModels;

public sealed partial class JavaViewModel(
    IJavaRuntimeManagementService javaRuntimeService) : PageViewModel
{
    public override string TitleKey => "Java.Title";
    public override string DescriptionKey => "Java.Description";

    public ObservableCollection<JavaRuntimeDescriptor> Runtimes { get; } = [];

    [ObservableProperty]
    private string _manualJavaPath = string.Empty;

    [RelayCommand(IncludeCancelCommand = true)]
    private Task LoadAsync(CancellationToken cancellationToken)
    {
        return RunAsync(ReloadRuntimesAsync, cancellationToken);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private Task ProvisionAsync(string? major, CancellationToken cancellationToken)
    {
        if (!int.TryParse(major, out var majorVersion))
        {
            StatusKey = "Java.InvalidMajor";
            return Task.CompletedTask;
        }

        return RunAsync(async token =>
        {
            var result = await javaRuntimeService.ProvisionMajorAsync(
                majorVersion,
                CreateProgress(),
                token);
            StatusKey = result.MessageKey;
            ErrorDetail = result.ErrorDetail;
            if (result.Succeeded)
            {
                await ReloadRuntimesAsync(token);
            }
        }, cancellationToken);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private Task ReverifyAsync(string? executablePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            StatusKey = "Java.RuntimeRequired";
            return Task.CompletedTask;
        }

        return RunAsync(async token =>
        {
            var result = await javaRuntimeService
                .ReverifyAsync(executablePath, token);
            ApplyOutcome(result);
            if (result.Succeeded)
            {
                await ReloadRuntimesAsync(token);
            }
        }, cancellationToken);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private Task DeleteManagedAsync(string? executablePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            StatusKey = "Java.RuntimeRequired";
            return Task.CompletedTask;
        }

        return RunAsync(async token =>
        {
            var result = await javaRuntimeService
                .DeleteManagedAsync(executablePath, token);
            ApplyOutcome(result);
            if (result.Succeeded)
            {
                await ReloadRuntimesAsync(token);
            }
        }, cancellationToken);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private Task UseAsManualAsync(string? executablePath, CancellationToken cancellationToken)
    {
        return SaveManualPathCoreAsync(executablePath, cancellationToken);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private Task SaveManualAsync(CancellationToken cancellationToken)
    {
        return SaveManualPathCoreAsync(ManualJavaPath, cancellationToken);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private Task ClearManualAsync(CancellationToken cancellationToken)
    {
        return SaveManualPathCoreAsync(null, cancellationToken);
    }

    public override Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        return LoadAsync(cancellationToken);
    }

    private Task SaveManualPathCoreAsync(
        string? executablePath,
        CancellationToken cancellationToken)
    {
        return RunAsync(async token =>
        {
            var result = await javaRuntimeService
                .SaveManualExecutablePathAsync(executablePath, token);
            ApplyOutcome(result);
            if (result.Succeeded)
            {
                await ReloadRuntimesAsync(token);
            }
        }, cancellationToken);
    }

    private async Task ReloadRuntimesAsync(CancellationToken cancellationToken)
    {
        var runtimes = await javaRuntimeService.GetRuntimesAsync(cancellationToken);
        Runtimes.Clear();
        foreach (var runtime in runtimes)
        {
            Runtimes.Add(runtime);
        }

        ManualJavaPath = await javaRuntimeService
            .GetManualExecutablePathAsync(cancellationToken) ?? string.Empty;
    }

    private void ApplyOutcome(JavaRuntimeManagementOutcome outcome)
    {
        StatusKey = outcome.MessageKey;
        ErrorDetail = outcome.ErrorDetail;
    }
}
