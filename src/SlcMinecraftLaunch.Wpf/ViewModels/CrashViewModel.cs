using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Wpf.ViewModels;

public sealed partial class CrashViewModel(
    IVersionService versionService,
    ICrashAnalysisService crashAnalysisService) : PageViewModel
{
    public override string TitleKey => "Crash.Title";
    public override string DescriptionKey => "Crash.Description";

    public ObservableCollection<string> Versions { get; } = [];
    public ObservableCollection<CrashInsight> Insights { get; } = [];

    [ObservableProperty]
    private string? _selectedVersion;

    [RelayCommand(IncludeCancelCommand = true)]
    private Task LoadAsync(CancellationToken cancellationToken)
    {
        return RunAsync(async token =>
        {
            var catalog = await versionService.GetCatalogAsync(token);
            Versions.Clear();
            foreach (var version in catalog.AllVersions.Select(entry => entry.Version.Id))
            {
                Versions.Add(version);
            }

            SelectedVersion ??= Versions.FirstOrDefault();
        }, cancellationToken);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private Task AnalyzeAsync(CancellationToken cancellationToken)
    {
        return RunAsync(async token =>
        {
            if (SelectedVersion is null)
            {
                throw new UiOperationException("Crash.SelectVersion");
            }

            var report = await crashAnalysisService.AnalyzeAsync(SelectedVersion, cancellationToken: token);
            Insights.Clear();
            foreach (var insight in report.Insights)
            {
                Insights.Add(insight);
            }
        }, cancellationToken);
    }

    public override Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        return LoadAsync(cancellationToken);
    }
}

