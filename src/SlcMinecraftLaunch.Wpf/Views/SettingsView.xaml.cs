using System.Diagnostics;
using System.IO;
using System.Windows.Controls;
using Microsoft.Win32;
using SlcMinecraftLaunch.Wpf.ViewModels;

namespace SlcMinecraftLaunch.Wpf.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private async void SaveCurseForgeKey_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel)
        {
            return;
        }

        await viewModel.SaveCurseForgeKeyCommand.ExecuteAsync(CurseForgeApiKeyBox.Password);
        if (viewModel.CurseForgeKeyStatusKey == "Settings.CurseForgeKeySaved")
        {
            CurseForgeApiKeyBox.Clear();
        }
    }

    private async void ClearCurseForgeKey_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel)
        {
            return;
        }

        await viewModel.ClearCurseForgeKeyCommand.ExecuteAsync(null);
        CurseForgeApiKeyBox.Clear();
    }

    private void OpenLogDirectory_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel ||
            string.IsNullOrWhiteSpace(viewModel.LogDirectory))
        {
            return;
        }

        Directory.CreateDirectory(viewModel.LogDirectory);
        Process.Start(new ProcessStartInfo(viewModel.LogDirectory) { UseShellExecute = true });
    }

    private async void CreateDiagnosticBundle_Click(
        object sender,
        System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "ZIP|*.zip",
            DefaultExt = ".zip",
            AddExtension = true,
            FileName = $"slc-diagnostics-{DateTime.Now:yyyyMMdd-HHmmss}.zip"
        };
        if (dialog.ShowDialog() == true)
        {
            await viewModel.CreateDiagnosticBundleCommand.ExecuteAsync(dialog.FileName);
        }
    }
}
