using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Wpf.ViewModels;

public sealed partial class JavaViewModel(
    IJavaDiscovery javaDiscovery,
    IJavaRuntimeService javaRuntimeService) : PageViewModel
{
    public override string TitleKey => "Java.Title";
    public override string DescriptionKey => "Java.Description";

    public ObservableCollection<JavaCandidate> Candidates { get; } = [];

    [RelayCommand(IncludeCancelCommand = true)]
    private Task LoadAsync(CancellationToken cancellationToken)
    {
        return RunAsync(async token =>
        {
            var candidates = await javaDiscovery.DiscoverAsync(token);
            Candidates.Clear();
            foreach (var candidate in candidates)
            {
                Candidates.Add(candidate);
            }
        }, cancellationToken);
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
                var candidates = await javaDiscovery.DiscoverAsync(token);
                Candidates.Clear();
                foreach (var candidate in candidates)
                {
                    Candidates.Add(candidate);
                }
            }
        }, cancellationToken);
    }

    public override Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        return LoadAsync(cancellationToken);
    }
}

