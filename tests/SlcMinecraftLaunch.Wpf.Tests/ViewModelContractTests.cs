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
    public async Task PageBusy_RemainsTrueUntilAllConcurrentOperationsFinish()
    {
        var viewModel = new ConcurrentPageViewModel();

        var first = viewModel.StartControlledAsync();
        var second = viewModel.StartControlledAsync();
        await viewModel.WaitForStartsAsync(2);

        viewModel.ReleaseNext();
        await viewModel.WaitForCompletionsAsync(1);

        Assert.True(viewModel.IsBusy);

        viewModel.ReleaseNext();
        await Task.WhenAll(first, second);

        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task HomeLaunch_ContinuesWhileRefreshesAreSkipped()
    {
        var launcher = new BlockingGameLaunchService();
        var viewModel = new HomeViewModel(null!, null!, null!, launcher)
        {
            SelectedVersion = "1.21.4",
            PlayerName = "OfflinePlayer"
        };

        var launch = viewModel.LaunchCommand.ExecuteAsync(null);
        await launcher.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        await viewModel.RefreshAsync().WaitAsync(TimeSpan.FromSeconds(1));

        Assert.True(viewModel.IsLaunching);
        Assert.True(viewModel.IsBusy);
        Assert.False(viewModel.CanLaunch);
        Assert.False(launch.IsCompleted);

        launcher.Release();
        await launch.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.False(viewModel.IsLaunching);
        Assert.True(viewModel.CanLaunch);
    }

    [Fact]
    public async Task DownloadCatalog_LoadsOnceAndCanInstallVanilla()
    {
        var catalog = new CountingVanillaVersionCatalogService();
        var downloads = new FakeDownloadTaskService();
        var viewModel = new DownloadLoaderViewModel(
            null!,
            null!,
            null!,
            catalog,
            null!,
            downloads,
            null!);

        await viewModel.RefreshAsync();
        await viewModel.RefreshAsync();

        Assert.Equal(1, catalog.CallCount);
        Assert.True(viewModel.IsCatalogLoaded);
        Assert.Equal("1.21.4", viewModel.SelectedCatalogVersion?.Id);
        Assert.True(viewModel.CanInstallVanilla);
        Assert.True(viewModel.InstallVanillaCommand.CanExecute(null));
    }

    [Fact]
    public async Task DownloadCatalog_RetriesAfterFirstFailure()
    {
        var catalog = new CountingVanillaVersionCatalogService(failuresBeforeSuccess: 1);
        var downloads = new FakeDownloadTaskService();
        var viewModel = new DownloadLoaderViewModel(
            null!,
            null!,
            null!,
            catalog,
            null!,
            downloads,
            null!);

        await viewModel.RefreshAsync();
        Assert.False(viewModel.IsCatalogLoaded);
        Assert.False(viewModel.CanInstallVanilla);

        await viewModel.RefreshAsync();

        Assert.Equal(2, catalog.CallCount);
        Assert.True(viewModel.IsCatalogLoaded);
        Assert.True(viewModel.CanInstallVanilla);
    }

    [Fact]
    public async Task Navigation_ChangesPageWithoutWaitingForActivation()
    {
        var catalog = new CountingVanillaVersionCatalogService(blocked: true);
        var downloads = new FakeDownloadTaskService();
        var download = new DownloadLoaderViewModel(
            null!,
            null!,
            null!,
            catalog,
            null!,
            downloads,
            null!);
        var home = new HomeViewModel(null!, null!, null!, null!);
        var shell = new ShellViewModel(
            home,
            null!,
            download,
            null!,
            null!,
            null!,
            null!,
            new FakeUiPreferences());
        var downloadItem = shell.NavigationItems.Single(item =>
            ReferenceEquals(item.Page, download));

        shell.NavigateCommand.Execute(downloadItem);

        Assert.Same(download, shell.CurrentPage);
        Assert.True(download.IsBusy);
        Assert.False(download.CanInstallVanilla);
        Assert.False(download.InstallVanillaCommand.CanExecute(null));

        catalog.Release();
        for (var attempt = 0; attempt < 200 && !download.IsCatalogLoaded; attempt++)
        {
            await Task.Delay(10);
        }

        Assert.True(download.IsCatalogLoaded);
        Assert.True(download.CanInstallVanilla);
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

    private sealed class ConcurrentPageViewModel : PageViewModel
    {
        private readonly Queue<TaskCompletionSource> _operations = [];
        private int _completed;

        public override string TitleKey => "Test.Title";
        public override string DescriptionKey => "Test.Description";

        public Task StartControlledAsync()
        {
            var completion = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _operations.Enqueue(completion);
            return RunAsync(
                _ => completion.Task.ContinueWith(
                    _ => Interlocked.Increment(ref _completed),
                    TaskScheduler.Default),
                CancellationToken.None);
        }

        public async Task WaitForStartsAsync(int count)
        {
            while (_operations.Count < count)
            {
                await Task.Delay(10);
            }
        }

        public void ReleaseNext()
        {
            _operations.Dequeue().TrySetResult();
        }

        public async Task WaitForCompletionsAsync(int count)
        {
            while (Volatile.Read(ref _completed) < count)
            {
                await Task.Delay(10);
            }
        }

        public override Task RefreshAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class BlockingGameLaunchService : IGameLaunchService
    {
        private readonly TaskCompletionSource _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<GameLaunchOutcome> LaunchAsync(
            string versionId,
            IProgress<OperationProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            Started.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken);
            return new GameLaunchOutcome(true, "Launch.Started");
        }

        public void Release() => _release.TrySetResult();
    }

    private sealed class CountingVanillaVersionCatalogService :
        IVanillaVersionCatalogService
    {
        private readonly TaskCompletionSource _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly bool _blocked;
        private int _remainingFailures;

        public CountingVanillaVersionCatalogService(
            int failuresBeforeSuccess = 0,
            bool blocked = false)
        {
            _remainingFailures = failuresBeforeSuccess;
            _blocked = blocked;
        }

        public int CallCount { get; private set; }

        public async Task<IReadOnlyList<MinecraftReleaseDescriptor>> GetVersionsAsync(
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (_blocked)
            {
                await _release.Task.WaitAsync(cancellationToken);
            }

            if (_remainingFailures-- > 0)
            {
                throw new InvalidOperationException("catalog unavailable");
            }

            return
            [
                new MinecraftReleaseDescriptor(
                    "1.21.4",
                    "release",
                    DateTimeOffset.UtcNow,
                    true,
                    false)
            ];
        }

        public void Release() => _release.TrySetResult();
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
}
