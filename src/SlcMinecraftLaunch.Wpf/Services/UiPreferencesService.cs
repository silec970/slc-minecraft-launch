using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using CommunityToolkit.Mvvm.ComponentModel;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Wpf.Services;

public sealed class UiPreferencesService : ObservableObject, IUiPreferences, IAsyncDisposable
{
    private readonly BackgroundMusicService _backgroundMusicService;
    private UiSettings _current = new();

    public UiPreferencesService(BackgroundMusicService backgroundMusicService)
    {
        _backgroundMusicService = backgroundMusicService;
        SystemParameters.StaticPropertyChanged += OnSystemParametersChanged;
    }

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
        Current = settings with
        {
            MusicVolume = Math.Clamp(settings.MusicVolume, 0, 1)
        };

        ApplyLanguage(settings.Language);
        ApplyTheme(settings.Theme);
        RefreshBindings();
        await _backgroundMusicService
            .ApplyAsync(Current, cancellationToken)
            .ConfigureAwait(true);
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
        var source = SystemParameters.HighContrast
            ? "Resources/Themes.HighContrast.xaml"
            : theme == UiTheme.Dark
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

    private static void RefreshBindings()
    {
        if (System.Windows.Application.Current.MainWindow is not { } window)
        {
            return;
        }

        RefreshBindings(window, new HashSet<DependencyObject>(ReferenceEqualityComparer.Instance));
        window.InvalidateVisual();
    }

    private static void RefreshBindings(
        DependencyObject dependencyObject,
        ISet<DependencyObject> visited)
    {
        if (!visited.Add(dependencyObject))
        {
            return;
        }

        var values = dependencyObject.GetLocalValueEnumerator();
        while (values.MoveNext())
        {
            var property = values.Current.Property;
            if (property is null)
            {
                continue;
            }

            BindingOperations.GetBindingExpression(dependencyObject, property)?.UpdateTarget();
        }

        foreach (var child in LogicalTreeHelper.GetChildren(dependencyObject).OfType<DependencyObject>())
        {
            RefreshBindings(child, visited);
        }

        if (dependencyObject is not Visual && dependencyObject is not Visual3D)
        {
            return;
        }

        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(dependencyObject); index++)
        {
            RefreshBindings(VisualTreeHelper.GetChild(dependencyObject, index), visited);
        }
    }

    private void OnSystemParametersChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(SystemParameters.HighContrast) ||
            System.Windows.Application.Current?.Dispatcher is not { } dispatcher)
        {
            return;
        }

        dispatcher.BeginInvoke(() =>
        {
            ApplyTheme(Current.Theme);
            RefreshBindings();
        });
    }

    public async ValueTask DisposeAsync()
    {
        SystemParameters.StaticPropertyChanged -= OnSystemParametersChanged;
        await _backgroundMusicService.DisposeAsync().ConfigureAwait(false);
    }
}
