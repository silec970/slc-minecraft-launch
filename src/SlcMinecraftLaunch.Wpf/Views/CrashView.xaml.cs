using System.Diagnostics;
using System.IO;
using System.Windows.Controls;
using SlcMinecraftLaunch.Wpf.ViewModels;

namespace SlcMinecraftLaunch.Wpf.Views;

public partial class CrashView : UserControl
{
    public CrashView()
    {
        InitializeComponent();
    }

    private void OpenCrashLog_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not CrashViewModel viewModel)
        {
            return;
        }

        var path = viewModel.SelectedHistory?.OriginalLogPath
                   ?? viewModel.Insights
                       .Select(insight => insight.Source)
                       .FirstOrDefault(File.Exists);
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return;
        }

        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private async void DeleteCrashReport_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not CrashViewModel viewModel || viewModel.SelectedHistory is null)
        {
            return;
        }

        var title = FindResource("Crash.History.DeleteConfirmTitle")?.ToString() ?? string.Empty;
        var message = FindResource("Crash.History.DeleteConfirmMessage")?.ToString() ?? string.Empty;
        var confirmation = System.Windows.MessageBox.Show(
            message,
            title,
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);
        if (confirmation == System.Windows.MessageBoxResult.Yes)
        {
            await viewModel.DeleteSelectedCommand.ExecuteAsync(null);
        }
    }
}

