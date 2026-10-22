using System.Windows.Controls;
using Microsoft.Win32;
using SlcMinecraftLaunch.Wpf.ViewModels;

namespace SlcMinecraftLaunch.Wpf.Views;

public partial class JavaView : UserControl
{
    public JavaView()
    {
        InitializeComponent();
    }

    private void SelectJavaExecutable_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not JavaViewModel viewModel)
        {
            return;
        }

        var executableLabel = FindResource("Java.ExecutableFiles")?.ToString()
                              ?? string.Empty;
        var dialog = new OpenFileDialog
        {
            Filter = $"java.exe|java.exe|{executableLabel}|*.exe",
            CheckFileExists = true,
            Multiselect = false
        };
        if (dialog.ShowDialog() == true)
        {
            viewModel.ManualJavaPath = dialog.FileName;
        }
    }

    private async void DeleteManagedJava_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string executablePath } ||
            DataContext is not JavaViewModel viewModel)
        {
            return;
        }

        var title = FindResource("Java.DeleteConfirmTitle")?.ToString() ?? string.Empty;
        var message = FindResource("Java.DeleteConfirmMessage")?.ToString() ?? string.Empty;
        var confirmation = System.Windows.MessageBox.Show(
            message,
            title,
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);
        if (confirmation == System.Windows.MessageBoxResult.Yes)
        {
            await viewModel.DeleteManagedCommand.ExecuteAsync(executablePath);
        }
    }
}

