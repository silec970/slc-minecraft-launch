using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SlcMinecraftLaunch.Domain.Abstractions;

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
        SettingsViewModel settings,
        IUiPreferences uiPreferences)
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
        Preferences = uiPreferences;
    }

    [ObservableProperty]
    private PageViewModel _currentPage;

    [ObservableProperty]
    private NavigationItem _selectedNavigation;

    public ObservableCollection<NavigationItem> NavigationItems { get; }
    public IUiPreferences Preferences { get; }

    [RelayCommand(IncludeCancelCommand = true)]
    private async Task NavigateAsync(NavigationItem? item, CancellationToken cancellationToken)
    {
        if (item is null)
        {
            return;
        }

        await NavigateToAsync(item, cancellationToken).ConfigureAwait(true);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private Task OpenSettingsAsync(CancellationToken cancellationToken)
    {
        var settingsItem = NavigationItems.FirstOrDefault(item =>
            item.LabelKey == "Navigation.Settings");
        return settingsItem is null
            ? Task.CompletedTask
            : NavigateToAsync(settingsItem, cancellationToken);
    }

    private async Task NavigateToAsync(
        NavigationItem item,
        CancellationToken cancellationToken)
    {
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
