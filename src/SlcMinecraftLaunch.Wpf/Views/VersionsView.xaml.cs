using System.Windows.Controls;
using Microsoft.Win32;
using SlcMinecraftLaunch.Wpf.ViewModels;

namespace SlcMinecraftLaunch.Wpf.Views;

public partial class VersionsView : UserControl
{
    public VersionsView()
    {
        InitializeComponent();
    }

    private async void DeleteInstance_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not VersionsViewModel viewModel)
        {
            return;
        }

        var title = FindResource("Instances.DeleteConfirmTitle")?.ToString() ?? string.Empty;
        var message = FindResource("Instances.DeleteConfirmMessage")?.ToString() ?? string.Empty;
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

    private async void ExportInstance_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not VersionsViewModel viewModel)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = CreateZipFilter(),
            DefaultExt = ".zip",
            AddExtension = true,
            FileName = $"{viewModel.SelectedInstance?.Name ?? "instance"}.zip"
        };
        if (dialog.ShowDialog() == true)
        {
            await viewModel.ExportSelectedCommand.ExecuteAsync(dialog.FileName);
        }
    }

    private async void BackupInstance_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not VersionsViewModel viewModel)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = CreateZipFilter(),
            DefaultExt = ".zip",
            AddExtension = true,
            FileName = $"{viewModel.SelectedInstance?.Name ?? "instance"}-backup.zip"
        };
        if (dialog.ShowDialog() == true)
        {
            await viewModel.SaveBackupCommand.ExecuteAsync(dialog.FileName);
        }
    }

    private async void ImportInstance_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not VersionsViewModel viewModel)
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Filter = CreateZipFilter(),
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog() == true)
        {
            await viewModel.ImportCommand.ExecuteAsync(dialog.FileName);
        }
    }

    private string CreateZipFilter()
    {
        var label = FindResource("Instances.ZipFiles")?.ToString() ?? string.Empty;
        return $"{label}|*.zip";
    }
}

