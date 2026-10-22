using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;
using SlcMinecraftLaunch.Wpf.ViewModels;

namespace SlcMinecraftLaunch.Wpf.Services;

public sealed class UiPreferencesService(
    BackgroundMusicService backgroundMusicService) : ObservableObject, IUiPreferences, IAsyncDisposable
{
    private UiSettings _current = new();

    public UiSettings Current
    {
        get => _current;
        private set
        {
            _current = value;
            OnPropertyChanged();
        }
    }

    public async Task ApplyAsync(
        UiSettings settings,
        CancellationToken cancellationToken = default)
    {
        var previousTheme = Current.Theme;
        var previousLanguage = Current.Language;
        Current = settings with
        {
            MusicVolume = Math.Clamp(settings.MusicVolume, 0, 1)
        };

        ApplyLanguage(settings.Language);
        ApplyTheme(settings.Theme);
        await backgroundMusicService
            .ApplyAsync(Current, cancellationToken)
            .ConfigureAwait(true);

        if (previousTheme != settings.Theme ||
            !string.Equals(previousLanguage, settings.Language, StringComparison.OrdinalIgnoreCase))
        {
            RecreateMainWindow();
        }
    }

    private static void ApplyLanguage(string language)
    {
        var source = string.Equals(language, "en-US", StringComparison.OrdinalIgnoreCase)
            ? "Resources/Strings.en-US.xaml"
            : "Resources/Strings.zh-CN.xaml";
        ReplaceDictionary("Resources/Strings.", source);
    }

    private static void ApplyTheme(UiTheme theme)
    {
        var source = theme == UiTheme.Dark
            ? "Resources/Themes.Dark.xaml"
            : "Resources/Themes.Light.xaml";
        ReplaceDictionary("Resources/Themes.", source);
    }

    private static void ReplaceDictionary(string prefix, string source)
    {
        var dictionaries = System.Windows.Application.Current.Resources.MergedDictionaries;
        var existing = dictionaries.FirstOrDefault(dictionary =>
            dictionary.Source?.OriginalString.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true);
        var replacement = new ResourceDictionary
        {
            Source = new Uri(source, UriKind.Relative)
        };

        if (existing is null)
        {
            dictionaries.Add(replacement);
            return;
        }

        var index = dictionaries.IndexOf(existing);
        dictionaries[index] = replacement;
    }

    private static void RecreateMainWindow()
    {
        if (System.Windows.Application.Current.MainWindow is not MainWindow current ||
            current.DataContext is not ShellViewModel shell)
        {
            return;
        }

        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            var replacement = new MainWindow
            {
                DataContext = shell
            };
            System.Windows.Application.Current.MainWindow = replacement;
            replacement.Show();
            current.Close();
        });
    }

    public async ValueTask DisposeAsync()
    {
        await backgroundMusicService.DisposeAsync().ConfigureAwait(false);
    }
}

