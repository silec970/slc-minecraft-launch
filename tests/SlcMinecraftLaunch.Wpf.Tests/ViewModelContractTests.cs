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
            new HomeViewModel(null!, null!, null!, null!),
            new VersionsViewModel(null!, null!, null!),
            new DownloadLoaderViewModel(
                null!,
                null!,
                null!,
                null!,
                null!,
                new FakeDownloadTaskService(),
                null!),
            new JavaViewModel(null!),
            new ModsViewModel(null!, null!),
            new CrashViewModel(null!, null!, null!),
            new SettingsViewModel(
                new FakeSettingsService(),
                new FakeUiPreferences(),
                new FakePlayerProfileService(),
                new FakeCurseForgeCredentialService(),
                new FakeDiagnosticBundleService())
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
        var home = new HomeViewModel(null!, null!, null!, null!);
        var versions = new VersionsViewModel(null!, null!, null!);
        var download = new DownloadLoaderViewModel(
            null!,
            null!,
            null!,
            null!,
            null!,
            new FakeDownloadTaskService(),
            null!);
        var java = new JavaViewModel(null!);
        var mods = new ModsViewModel(null!, null!);
        var crash = new CrashViewModel(null!, null!, null!);
        var settings = new SettingsViewModel(
            new FakeSettingsService(),
            new FakeUiPreferences(),
            new FakePlayerProfileService(),
            new FakeCurseForgeCredentialService(),
            new FakeDiagnosticBundleService());
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
        var playerProfile = new FakePlayerProfileService();
        var credentialService = new FakeCurseForgeCredentialService();
        var viewModel = new SettingsViewModel(
            settingsService,
            preferences,
            playerProfile,
            credentialService,
            new FakeDiagnosticBundleService())
        {
            IsDarkTheme = true,
            AnimationsEnabled = false,
            BackgroundEnabled = false,
            MusicEnabled = true,
            MusicVolume = 0.3,
            PlayerName = "OfflinePlayer",
            SelectedLanguage = new LanguageOption("en-US", "Settings.LanguageEnglish")
        };

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.Equal(UiTheme.Dark, settingsService.Saved?.Theme);
        Assert.Equal("en-US", settingsService.Saved?.Language);
        Assert.False(preferences.Current.AnimationsEnabled);
        Assert.True(preferences.Current.MusicEnabled);
        Assert.Equal("OfflinePlayer", playerProfile.SavedPlayerName);
    }

    [Fact]
    public async Task SettingsSave_UpdatesCurseForgeCredentialStatus()
    {
        var credentialService = new FakeCurseForgeCredentialService();
        var viewModel = new SettingsViewModel(
            new FakeSettingsService(),
            new FakeUiPreferences(),
            new FakePlayerProfileService(),
            credentialService,
            new FakeDiagnosticBundleService());

        await viewModel.SaveCurseForgeKeyCommand.ExecuteAsync("cf-test-secret");

        Assert.Equal("cf-test-secret", credentialService.SavedApiKey);
        Assert.True(viewModel.HasCurseForgeKey);
        Assert.Equal("Settings.CurseForgeKeySaved", viewModel.CurseForgeKeyStatusKey);
    }

    [Fact]
    public void Home_EnablesLaunchOnlyWithVersionAndPlayerName()
    {
        var viewModel = new HomeViewModel(null!, null!, null!, null!)
        {
            SelectedVersion = "1.21.4"
        };

        Assert.False(viewModel.CanLaunch);

        viewModel.PlayerName = "OfflinePlayer";

        Assert.True(viewModel.CanLaunch);
    }

    [Fact]
    public async Task ModsSearch_WithoutCurseForgeKey_ShowsLocalizedGuidanceWithoutThrowing()
    {
        var viewModel = new ModsViewModel(new FakeVersionService(), new FakeModService())
        {
            SelectedVersion = "1.21.4",
            SearchText = "sodium"
        };

        await viewModel.SearchCommand.ExecuteAsync(null);

        Assert.Equal("Mods.CurseForgeKeyMissing", viewModel.StatusKey);
        Assert.Null(viewModel.ErrorDetail);
        Assert.Empty(viewModel.SearchResults);
    }

    [Fact]
    public async Task CancelCurrentOperation_StopsRunningPageWork()
    {
        var viewModel = new CancellablePageViewModel();

        var running = viewModel.StartAsync();
        await viewModel.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(viewModel.CanCancelCurrentOperation);
        Assert.True(viewModel.CancelCurrentOperationCommand.CanExecute(null));

        viewModel.CancelCurrentOperationCommand.Execute(null);
        await running.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal("Status.Cancelled", viewModel.StatusKey);
        Assert.False(viewModel.IsBusy);
        Assert.False(viewModel.CanCancelCurrentOperation);
        Assert.False(viewModel.CancelCurrentOperationCommand.CanExecute(null));
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

    private sealed class FakePlayerProfileService : IPlayerProfileService
    {
        public string? SavedPlayerName { get; private set; }

        public Task<string> GetPlayerNameAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(string.Empty);

        public Task<PlayerProfileSaveResult> SavePlayerNameAsync(
            string playerName,
            CancellationToken cancellationToken = default)
        {
            SavedPlayerName = playerName;
            return Task.FromResult(new PlayerProfileSaveResult(
                true,
                "Settings.PlayerNameSaved",
                playerName));
        }
    }

    private sealed class FakeCurseForgeCredentialService : ICurseForgeCredentialService
    {
        public bool IsConfigured { get; private set; }
        public string? SavedApiKey { get; private set; }

        public Task<CurseForgeCredentialStatus> LoadStatusAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(new CurseForgeCredentialStatus(
                IsConfigured,
                IsConfigured
                    ? "Settings.CurseForgeKeyConfigured"
                    : "Settings.CurseForgeKeyMissing"));

        public Task<CurseForgeCredentialStatus> SaveApiKeyAsync(
            string apiKey,
            CancellationToken cancellationToken = default)
        {
            SavedApiKey = apiKey;
            IsConfigured = !string.IsNullOrWhiteSpace(apiKey);
            return Task.FromResult(new CurseForgeCredentialStatus(
                IsConfigured,
                IsConfigured
                    ? "Settings.CurseForgeKeySaved"
                    : "Settings.CurseForgeKeyRequired"));
        }

        public Task<CurseForgeCredentialStatus> ClearApiKeyAsync(
            CancellationToken cancellationToken = default)
        {
            SavedApiKey = null;
            IsConfigured = false;
            return Task.FromResult(new CurseForgeCredentialStatus(
                false,
                "Settings.CurseForgeKeyCleared"));
        }
    }

    private sealed class FakeDownloadTaskService : IDownloadTaskService
    {
        public event EventHandler<DownloadTaskRecord>? TaskChanged
        {
            add { }
            remove { }
        }

        public string? ActiveTaskId => null;

        public Task<IReadOnlyList<DownloadTaskRecord>> GetTasksAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DownloadTaskRecord>>([]);

        public Task<DownloadTaskRecord> StartAsync(
            ResourceCompletionRequest request,
            IProgress<OperationProgress>? progress = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DownloadTaskRecord?> PauseAsync(
            string taskId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DownloadTaskRecord> ResumeAsync(
            string taskId,
            IProgress<OperationProgress>? progress = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<DownloadTaskRecord> RetryFailedAsync(
            string taskId,
            IProgress<OperationProgress>? progress = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> DeleteAsync(
            string taskId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeVersionService : IVersionService
    {
        public Task<GameVersionCatalog> GetCatalogAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(new GameVersionCatalog(@"C:\Minecraft", []));

        public Task SetFavoriteAsync(
            string versionId,
            bool isFavorite,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SetIsolationAsync(
            string versionId,
            bool isIsolated,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeModService : IModService
    {
        public Task<ModSearchOutcome> SearchAsync(
            ModSearchQuery query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ModSearchOutcome(
                [],
                ["Mods.CurseForgeKeyMissing"]));

        public Task<IReadOnlyList<ModEntry>> ScanAsync(
            string versionId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ModEntry>>([]);

        public Task<IReadOnlyList<ModConflict>> DetectConflictsAsync(
            string versionId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ModConflict>>([]);

        public Task<ModInstallOutcome> InstallLocalAsync(
            LocalModInstallRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ModInstallOutcome> InstallRemoteAsync(
            ModInstallRequest request,
            IProgress<OperationProgress>? progress = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ModInstallOutcome> InstallRecommendedDependencyAsync(
            string versionId,
            LoaderKind loaderKind,
            ModConflict conflict,
            IProgress<OperationProgress>? progress = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeDiagnosticBundleService : IDiagnosticBundleService
    {
        public Task<string> CreateAsync(
            string destinationPath,
            CancellationToken cancellationToken = default)
            => Task.FromResult(destinationPath);
    }

    private sealed class CancellablePageViewModel : PageViewModel
    {
        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override string TitleKey => "Test.Title";
        public override string DescriptionKey => "Test.Description";

        public Task StartAsync()
        {
            return RunAsync(async cancellationToken =>
            {
                Started.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }, CancellationToken.None);
        }

        public override Task RefreshAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
