using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Wpf.ViewModels;

public sealed partial class SettingsViewModel(
    ISettingsService settingsService,
    IUiPreferences uiPreferences,
    IPlayerProfileService playerProfileService,
    ICurseForgeCredentialService curseForgeCredentials) : PageViewModel
{
    public override string TitleKey => "Settings.Title";
    public override string DescriptionKey => "Settings.Description";

    public IReadOnlyList<LanguageOption> Languages { get; } =
    [
        new("zh-CN", "Settings.LanguageChinese"),
        new("en-US", "Settings.LanguageEnglish")
    ];

    [ObservableProperty]
    private string _gameRoot = string.Empty;

    [ObservableProperty]
    private string _dataRoot = string.Empty;

    [ObservableProperty]
    private string _launcherVersion = string.Empty;

    [ObservableProperty]
    private LanguageOption? _selectedLanguage;

    [ObservableProperty]
    private bool _isDarkTheme;

    [ObservableProperty]
    private bool _animationsEnabled = true;

    [ObservableProperty]
    private bool _backgroundEnabled = true;

    [ObservableProperty]
    private bool _musicEnabled;

    [ObservableProperty]
    private double _musicVolume = 0.45;

    [ObservableProperty]
    private string _playerName = string.Empty;

    [ObservableProperty]
    private bool _hasCurseForgeKey;

    [ObservableProperty]
    private string _curseForgeKeyStatusKey = "Settings.CurseForgeKeyMissing";

    [RelayCommand(IncludeCancelCommand = true)]
    private Task SaveAsync(CancellationToken cancellationToken)
    {
        return RunAsync(async token =>
        {
            var playerResult = await playerProfileService
                .SavePlayerNameAsync(PlayerName, token);
            if (!playerResult.Succeeded)
            {
                StatusKey = playerResult.MessageKey;
                return;
            }

            PlayerName = playerResult.PlayerName;
            var settings = new UiSettings(
                IsDarkTheme ? UiTheme.Dark : UiTheme.Light,
                SelectedLanguage?.Code ?? "zh-CN",
                AnimationsEnabled,
                BackgroundEnabled,
                MusicEnabled,
                MusicVolume);
            await settingsService.SaveUiSettingsAsync(settings, token);
            await uiPreferences.ApplyAsync(settings, token);
            StatusKey = "Settings.Saved";
        }, cancellationToken);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private Task SaveCurseForgeKeyAsync(
        string? apiKey,
        CancellationToken cancellationToken)
    {
        return RunAsync(async token =>
        {
            var status = await curseForgeCredentials
                .SaveApiKeyAsync(apiKey ?? string.Empty, token);
            ApplyCredentialStatus(status);
            StatusKey = status.MessageKey;
        }, cancellationToken);
    }

    [RelayCommand(IncludeCancelCommand = true)]
    private Task ClearCurseForgeKeyAsync(CancellationToken cancellationToken)
    {
        return RunAsync(async token =>
        {
            var status = await curseForgeCredentials
                .ClearApiKeyAsync(token);
            ApplyCredentialStatus(status);
            StatusKey = status.MessageKey;
        }, cancellationToken);
    }

    public override async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = settingsService.GetSnapshot();
        GameRoot = snapshot.GameRoot;
        DataRoot = snapshot.DataRoot;
        LauncherVersion = snapshot.Version;
        var settings = await settingsService.LoadUiSettingsAsync(cancellationToken);
        PlayerName = await playerProfileService.GetPlayerNameAsync(cancellationToken);
        IsDarkTheme = settings.Theme == UiTheme.Dark;
        AnimationsEnabled = settings.AnimationsEnabled;
        BackgroundEnabled = settings.BackgroundEnabled;
        MusicEnabled = settings.MusicEnabled;
        MusicVolume = settings.MusicVolume;
        SelectedLanguage = Languages.FirstOrDefault(language =>
                              string.Equals(language.Code, settings.Language, StringComparison.OrdinalIgnoreCase))
                          ?? Languages[0];
        ApplyCredentialStatus(
            await curseForgeCredentials.LoadStatusAsync(cancellationToken));
        StatusKey = "Status.Ready";
    }

    private void ApplyCredentialStatus(CurseForgeCredentialStatus status)
    {
        HasCurseForgeKey = status.IsConfigured;
        CurseForgeKeyStatusKey = status.MessageKey;
    }
}

public sealed record LanguageOption(string Code, string LabelKey);
