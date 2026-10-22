using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using SlcMinecraftLaunch.Wpf.ViewModels;

namespace SlcMinecraftLaunch.Wpf;

public partial class MainWindow : Window
{
    private ShellViewModel? _shellViewModel;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_shellViewModel is not null)
        {
            _shellViewModel.PropertyChanged -= OnShellPropertyChanged;
        }

        _shellViewModel = e.NewValue as ShellViewModel;
        if (_shellViewModel is not null)
        {
            _shellViewModel.PropertyChanged += OnShellPropertyChanged;
        }
    }

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ShellViewModel.CurrentPage) ||
            _shellViewModel?.Preferences.Current.AnimationsEnabled != true)
        {
            return;
        }

        PageHost.BeginAnimation(OpacityProperty, null);
        PageHost.Opacity = 0.55;
        var translate = (TranslateTransform)PageHost.RenderTransform;
        translate.Y = 10;
        PageHost.BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(0.55, 1, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });
        translate.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(180))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });
    }
}
