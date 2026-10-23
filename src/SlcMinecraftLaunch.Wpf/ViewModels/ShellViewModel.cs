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

    [RelayCommand]
    private void Navigate(NavigationItem? item)
    {
        if (item is null)
        {
            return;
        }

        NavigateTo(item);
    }

    [RelayCommand]
    private void OpenSettings()
    {
        var settingsItem = NavigationItems.FirstOrDefault(item =>
            item.LabelKey == "Navigation.Settings");
        if (settingsItem is not null)
        {
            NavigateTo(settingsItem);
        }
    }

    private void NavigateTo(NavigationItem item)
    {
        SelectedNavigation = item;
        CurrentPage = item.Page;
        _ = ActivatePageAsync(item.Page);
    }

    private static async Task ActivatePageAsync(PageViewModel page)
    {
        try
        {
            await page.RefreshAsync().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Navigation does not own page-operation cancellation.
        }
        catch (Exception exception)
        {
            page.ReportActivationFailure(exception);
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        CurrentPage = SelectedNavigation.Page;
        await CurrentPage.RefreshAsync(cancellationToken);
    }
}
