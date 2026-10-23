using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SlcMinecraftLaunch.Application;
using SlcMinecraftLaunch.Domain.Abstractions;

namespace SlcMinecraftLaunch.Wpf.ViewModels;

public sealed partial class HomeViewModel(
    LaunchApplicationService launchApplicationService,
    IVersionService versionService,
    IPlayerProfileService playerProfileService,
    IGameLaunchService gameLaunchService) : PageViewModel
{
    public override string TitleKey => "Home.Title";
    public override string DescriptionKey => "Home.Description";

    public ObservableCollection<string> InstalledVersions { get; } = [];

    [ObservableProperty]
    private string _rootPath = string.Empty;

    [ObservableProperty]
    private int _installedVersionCount;

    [ObservableProperty]
    private string? _selectedVersion;

    [ObservableProperty]
    private string _playerName = string.Empty;

    [ObservableProperty]
    private bool _isLaunching;

    public bool CanLaunch => !string.IsNullOrWhiteSpace(SelectedVersion) &&
                             !string.IsNullOrWhiteSpace(PlayerName) &&
                             !IsLaunching &&
                             !IsBusy;

    partial void OnSelectedVersionChanged(string? value)
    {
        OnPropertyChanged(nameof(CanLaunch));
    }

    partial void OnPlayerNameChanged(string value)
    {
        OnPropertyChanged(nameof(CanLaunch));
    }

    partial void OnIsLaunchingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanLaunch));
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.PropertyName == nameof(IsBusy))
        {
            OnPropertyChanged(nameof(CanLaunch));
        }
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private Task LoadAsync(CancellationToken cancellationToken)
    {
        return RunAsync(async token =>
        {
            var snapshot = await launchApplicationService.GetSnapshotAsync(token);
            var catalog = await versionService.GetCatalogAsync(token);
            PlayerName = await playerProfileService.GetPlayerNameAsync(token);
            RootPath = snapshot.RootPath;
            InstalledVersionCount = snapshot.VersionIds.Count;
            InstalledVersions.Clear();
            foreach (var version in catalog.AllVersions.Select(entry => entry.Version.Id))
            {
                InstalledVersions.Add(version);
            }

            SelectedVersion = InstalledVersions.FirstOrDefault();
        }, cancellationToken);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task LaunchAsync(CancellationToken cancellationToken)
    {
        IsLaunching = true;
        try
        {
            await RunAsync(async token =>
            {
                if (string.IsNullOrWhiteSpace(SelectedVersion))
                {
                    throw new UiOperationException("Launch.VersionRequired");
                }

                if (string.IsNullOrWhiteSpace(PlayerName))
                {
                    throw new UiOperationException("Launch.PlayerNameRequired");
                }

                var result = await gameLaunchService.LaunchAsync(
                    SelectedVersion,
                    CreateProgress(),
                    token);
                StatusKey = result.MessageKey;
                ErrorDetail = result.ErrorDetail;
            }, cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            IsLaunching = false;
        }
    }

    public override Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        return IsLaunching || IsBusy
            ? Task.CompletedTask
            : LoadAsync(cancellationToken);
    }
}
