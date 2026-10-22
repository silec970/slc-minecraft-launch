using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Wpf.ViewModels;

public sealed partial class SettingsViewModel(
    ISettingsService settingsService,
    IUiPreferences uiPreferences) : PageViewModel
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

    [RelayCommand(IncludeCancelCommand = true)]
    private Task SaveAsync(CancellationToken cancellationToken)
    {
        return RunAsync(async token =>
        {
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

    public override async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = settingsService.GetSnapshot();
        GameRoot = snapshot.GameRoot;
        DataRoot = snapshot.DataRoot;
        LauncherVersion = snapshot.Version;
        var settings = await settingsService.LoadUiSettingsAsync(cancellationToken);
        IsDarkTheme = settings.Theme == UiTheme.Dark;
        AnimationsEnabled = settings.AnimationsEnabled;
        BackgroundEnabled = settings.BackgroundEnabled;
        MusicEnabled = settings.MusicEnabled;
        MusicVolume = settings.MusicVolume;
        SelectedLanguage = Languages.FirstOrDefault(language =>
                              string.Equals(language.Code, settings.Language, StringComparison.OrdinalIgnoreCase))
                          ?? Languages[0];
        StatusKey = "Status.Ready";
    }
}

public sealed record LanguageOption(string Code, string LabelKey);
