namespace SlcMinecraftLaunch.Wpf.ViewModels;

public sealed record NavigationItem(
    string LabelKey,
    string Glyph,
    PageViewModel Page);

