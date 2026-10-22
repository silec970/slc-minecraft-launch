using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace SlcMinecraftLaunch.Wpf.ViewModels;

public sealed partial class ShellViewModel : ObservableObject
{
    public ShellViewModel(
        HomeViewModel home,
        VersionsViewModel versions,
        DownloadLoaderViewModel downloadLoader,
        JavaViewModel java,
        ModsViewModel mods,
        CrashViewModel crash,
        SettingsViewModel settings)
    {
        NavigationItems =
        [
            new("Navigation.Home", "\uE80F", home),
            new("Navigation.Versions", "\uE81E", versions),
            new("Navigation.Download", "\uE896", downloadLoader),
            new("Navigation.Java", "\uE943", java),
            new("Navigation.Mods", "\uE7B8", mods),
            new("Navigation.Crash", "\uE7BA", crash),
            new("Navigation.Settings", "\uE713", settings)
        ];
        _currentPage = home;
        _selectedNavigation = NavigationItems[0];
    }

    [ObservableProperty]
    private PageViewModel _currentPage;

    [ObservableProperty]
    private NavigationItem _selectedNavigation;

    [ObservableProperty]
    private string _accountStatusKey = "Home.MicrosoftAccountRequired";

    public ObservableCollection<NavigationItem> NavigationItems { get; }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task NavigateAsync(NavigationItem? item, CancellationToken cancellationToken)
    {
        if (item is null)
        {
            return;
        }

        SelectedNavigation = item;
        CurrentPage = item.Page;
        await CurrentPage.RefreshAsync(cancellationToken);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        CurrentPage = SelectedNavigation.Page;
        await CurrentPage.RefreshAsync(cancellationToken);
    }
}
