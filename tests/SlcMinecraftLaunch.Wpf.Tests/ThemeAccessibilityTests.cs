using System.Globalization;
using System.Xml.Linq;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Tests;

public sealed class ThemeAccessibilityTests
{
    private static readonly XNamespace Xaml =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    [Fact]
    public void ThemeDictionaries_ExposeMatchingKeysAndAccessibleContrast()
    {
        var themes = new[]
        {
            LoadTheme("Themes.Light.xaml"),
            LoadTheme("Themes.Dark.xaml"),
            LoadTheme("Themes.HighContrast.xaml")
        };
        var keys = themes.Select(theme => theme.Keys.Order().ToArray()).ToArray();

        Assert.Equal(keys[0], keys[1]);
        Assert.Equal(keys[0], keys[2]);
        Assert.All(themes, theme =>
        {
            AssertContrast(theme, "Brush.Ink", "Brush.Surface", 4.5);
            AssertContrast(theme, "Brush.Ink", "Brush.Card", 4.5);
            AssertContrast(theme, "Brush.OnAccent", "Brush.Accent", 4.5);
            AssertContrast(theme, "Brush.SidebarText", "Brush.Sidebar", 4.5);
        });
        AssertContrast(themes[2], "Brush.Ink", "Brush.Surface", 7);
    }

    [Fact]
    public void Views_UseDynamicThemeBrushesAndInPlacePreferences()
    {
        var wpfRoot = Path.Combine(GetRepositoryRoot(), "src", "SlcMinecraftLaunch.Wpf");
        var xaml = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(wpfRoot, "*.xaml", SearchOption.AllDirectories)
                .Select(File.ReadAllText));
        var preferences = File.ReadAllText(Path.Combine(
            wpfRoot,
            "Services",
            "UiPreferencesService.cs"));

        Assert.DoesNotContain("{StaticResource Brush.", xaml);
        Assert.DoesNotContain("Foreground=\"#", xaml);
        Assert.Contains("KeyboardNavigation.TabNavigation=\"Cycle\"", xaml);
        Assert.Contains("AutomationProperties.Name", xaml);
        Assert.DoesNotContain("RecreateMainWindow", preferences);
    }

    private static ThemePalette LoadTheme(string fileName)
    {
        var path = Path.Combine(
            GetRepositoryRoot(),
            "src",
            "SlcMinecraftLaunch.Wpf",
            "Resources",
            fileName);
        var document = XDocument.Load(path);
        var colors = document.Root!.Elements()
            .Where(element => element.Name.LocalName == "SolidColorBrush")
            .ToDictionary(
                element => (string)element.Attribute(Xaml + "Key")!,
                element => ParseColor((string)element.Attribute("Color")!),
                StringComparer.Ordinal);
        return new ThemePalette(colors);
    }

    private static void AssertContrast(
        ThemePalette theme,
        string foregroundKey,
        string backgroundKey,
        double minimum)
    {
        var ratio = ContrastRatio(theme[foregroundKey], theme[backgroundKey]);
        Assert.True(
            ratio >= minimum,
            $"{foregroundKey}/{backgroundKey} contrast {ratio:F2} is below {minimum:F1}");
    }

    private static double ContrastRatio(Color foreground, Color background)
    {
        var lighter = Math.Max(Luminance(foreground), Luminance(background));
        var darker = Math.Min(Luminance(foreground), Luminance(background));
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double Luminance(Color color)
    {
        static double Channel(byte value)
        {
            var normalized = value / 255d;
            return normalized <= 0.03928
                ? normalized / 12.92
                : Math.Pow((normalized + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(color.R)) +
               (0.7152 * Channel(color.G)) +
               (0.0722 * Channel(color.B));
    }

    private static Color ParseColor(string value)
    {
        var hex = value.TrimStart('#');
        if (hex.Length == 8)
        {
            hex = hex[2..];
        }

        if (hex.Length != 6)
        {
            throw new FormatException($"Unsupported color '{value}'.");
        }

        return new Color(
            byte.Parse(hex[..2], NumberStyles.HexNumber),
            byte.Parse(hex[2..4], NumberStyles.HexNumber),
            byte.Parse(hex[4..6], NumberStyles.HexNumber));
    }

    private static string GetRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "SlcMinecraftLaunch.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
               ?? throw new DirectoryNotFoundException("Repository root not found.");
    }

    private sealed class ThemePalette(IReadOnlyDictionary<string, Color> colors)
    {
        public IReadOnlyCollection<string> Keys => colors.Keys.ToArray();
        public Color this[string key] => colors[key];
    }

    private readonly record struct Color(byte R, byte G, byte B);
}
