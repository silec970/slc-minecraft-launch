using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SlcMinecraftLaunch.Application;
using SlcMinecraftLaunch.Domain.Abstractions;

namespace SlcMinecraftLaunch.Wpf.ViewModels;

public sealed partial class HomeViewModel(
    LaunchApplicationService launchApplicationService,
    IVersionService versionService) : PageViewModel
{
    public override string TitleKey => "Home.Title";
    public override string DescriptionKey => "Home.Description";

    [ObservableProperty]
    private string _rootPath = string.Empty;

    [ObservableProperty]
    private int _installedVersionCount;

    [ObservableProperty]
    private string _primaryVersion = string.Empty;

    [RelayCommand(IncludeCancelCommand = true)]
    private Task LoadAsync(CancellationToken cancellationToken)
    {
        return RunAsync(async token =>
        {
            var snapshot = await launchApplicationService.GetSnapshotAsync(token);
            var catalog = await versionService.GetCatalogAsync(token);
            RootPath = snapshot.RootPath;
            InstalledVersionCount = snapshot.VersionIds.Count;
            PrimaryVersion = catalog.AllVersions.FirstOrDefault()?.Version.Id ?? string.Empty;
        }, cancellationToken);
    }

    public override Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        return LoadAsync(cancellationToken);
    }
}

