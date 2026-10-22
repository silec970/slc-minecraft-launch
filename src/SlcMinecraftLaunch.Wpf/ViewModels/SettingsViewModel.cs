using CommunityToolkit.Mvvm.ComponentModel;
using SlcMinecraftLaunch.Domain.Abstractions;

namespace SlcMinecraftLaunch.Wpf.ViewModels;

public sealed partial class SettingsViewModel(ISettingsService settingsService) : PageViewModel
{
    public override string TitleKey => "Settings.Title";
    public override string DescriptionKey => "Settings.Description";

    [ObservableProperty]
    private string _gameRoot = string.Empty;

    [ObservableProperty]
    private string _dataRoot = string.Empty;

    [ObservableProperty]
    private string _launcherVersion = string.Empty;

    [ObservableProperty]
    private string _languageKey = "Settings.LanguageChinese";

    public override Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = settingsService.GetSnapshot();
        GameRoot = snapshot.GameRoot;
        DataRoot = snapshot.DataRoot;
        LauncherVersion = snapshot.Version;
        StatusKey = "Status.Ready";
        return Task.CompletedTask;
    }
}
