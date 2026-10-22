using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Wpf.ViewModels;

public sealed partial class CrashViewModel(
    IVersionService versionService,
    ICrashAnalysisService crashAnalysisService,
    ICrashHistoryService crashHistoryService) : PageViewModel
{
    public override string TitleKey => "Crash.Title";
    public override string DescriptionKey => "Crash.Description";

    public ObservableCollection<string> Versions { get; } = [];
    public ObservableCollection<CrashInsight> Insights { get; } = [];
    public ObservableCollection<CrashHistoryItem> History { get; } = [];

    [ObservableProperty]
    private string? _selectedVersion;

    [ObservableProperty]
    private CrashHistoryItem? _selectedHistory;

    public bool HasSelectedHistory => SelectedHistory is not null;

    partial void OnSelectedHistoryChanged(CrashHistoryItem? value)
    {
        OnPropertyChanged(nameof(HasSelectedHistory));
        if (value is not null)
        {
            ApplyReport(value.Report);
            SelectedVersion = value.Report.VersionId;
        }
    }

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
            await ReloadHistoryAsync(token);
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

            var report = await crashAnalysisService.AnalyzeAsync(
                SelectedVersion,
                cancellationToken: token);
            ApplyReport(report);
            await ReloadHistoryAsync(token, report.ReportId);
        }, cancellationToken);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private Task DeleteSelectedAsync(CancellationToken cancellationToken)
    {
        return RunAsync(async token =>
        {
            if (SelectedHistory is null)
            {
                throw new UiOperationException("Crash.History.SelectRequired");
            }

            if (!await crashHistoryService.DeleteAsync(SelectedHistory.ReportId, token))
            {
                throw new UiOperationException("Crash.History.DeleteFailed");
            }

            SelectedHistory = null;
            Insights.Clear();
            StatusKey = "Crash.History.Deleted";
            await ReloadHistoryAsync(token);
        }, cancellationToken);
    }

    public override Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        return LoadAsync(cancellationToken);
    }

    private async Task ReloadHistoryAsync(
        CancellationToken cancellationToken,
        string? selectedReportId = null)
    {
        selectedReportId ??= SelectedHistory?.ReportId;
        var history = await crashHistoryService.GetHistoryAsync(
            cancellationToken: cancellationToken);
        History.Clear();
        foreach (var item in history)
        {
            History.Add(item);
        }

        SelectedHistory = History.FirstOrDefault(item =>
                               string.Equals(
                                   item.ReportId,
                                   selectedReportId,
                                   StringComparison.OrdinalIgnoreCase))
                           ?? History.FirstOrDefault();
    }

    private void ApplyReport(CrashAnalysisReport report)
    {
        Insights.Clear();
        foreach (var insight in report.Insights)
        {
            Insights.Add(insight);
        }
    }
}
