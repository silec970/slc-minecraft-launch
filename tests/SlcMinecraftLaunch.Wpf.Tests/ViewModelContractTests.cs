using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;
using SlcMinecraftLaunch.Wpf.ViewModels;

namespace SlcMinecraftLaunch.Tests;

public sealed class ViewModelContractTests
{
    [Fact]
    public void PageViewModels_ExposeLocalizationKeys()
    {
        PageViewModel[] pages =
        [
            new HomeViewModel(null!, null!),
            new VersionsViewModel(null!),
            new DownloadLoaderViewModel(null!, null!, null!, null!, null!),
            new JavaViewModel(null!, null!),
            new ModsViewModel(null!, null!),
            new CrashViewModel(null!, null!),
            new SettingsViewModel(new FakeSettingsService(), new FakeUiPreferences())
        ];

        Assert.All(pages, page =>
        {
            Assert.Matches("^[A-Za-z.]+$", page.TitleKey);
            Assert.Matches("^[A-Za-z.]+$", page.DescriptionKey);
            Assert.Equal("Status.Ready", page.StatusKey);
        });
    }

    [Fact]
    public void Shell_ContainsOnePagePerWorkflowModule()
    {
        var home = new HomeViewModel(null!, null!);
        var versions = new VersionsViewModel(null!);
        var download = new DownloadLoaderViewModel(null!, null!, null!, null!, null!);
        var java = new JavaViewModel(null!, null!);
        var mods = new ModsViewModel(null!, null!);
        var crash = new CrashViewModel(null!, null!);
        var settings = new SettingsViewModel(new FakeSettingsService(), new FakeUiPreferences());
        var preferences = new FakeUiPreferences();

        var shell = new ShellViewModel(home, versions, download, java, mods, crash, settings, preferences);

        Assert.Equal(7, shell.NavigationItems.Count);
        Assert.Same(home, shell.CurrentPage);
        Assert.Contains(shell.NavigationItems, item => item.Page is ModsViewModel);
        Assert.Contains(shell.NavigationItems, item => item.Page is CrashViewModel);
    }

    [Fact]
    public async Task SettingsSave_PersistsAndAppliesUiPreferences()
    {
        var settingsService = new FakeSettingsService();
        var preferences = new FakeUiPreferences();
        var viewModel = new SettingsViewModel(settingsService, preferences)
        {
            IsDarkTheme = true,
            AnimationsEnabled = false,
            BackgroundEnabled = false,
            MusicEnabled = true,
            MusicVolume = 0.3,
            SelectedLanguage = new LanguageOption("en-US", "Settings.LanguageEnglish")
        };

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal(UiTheme.Dark, settingsService.Saved?.Theme);
        Assert.Equal("en-US", settingsService.Saved?.Language);
        Assert.False(preferences.Current.AnimationsEnabled);
        Assert.True(preferences.Current.MusicEnabled);
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public UiSettings? Saved { get; private set; }

        public LauncherSettingsSnapshot GetSnapshot()
            => new(@"C:\Minecraft", @"C:\Data", "test");

        public Task<UiSettings> LoadUiSettingsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new UiSettings());

        public Task SaveUiSettingsAsync(
            UiSettings settings,
            CancellationToken cancellationToken = default)
        {
            Saved = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUiPreferences : IUiPreferences
    {
        public UiSettings Current { get; private set; } = new();

        public Task ApplyAsync(UiSettings settings, CancellationToken cancellationToken = default)
        {
            Current = settings;
            return Task.CompletedTask;
        }
    }
}
