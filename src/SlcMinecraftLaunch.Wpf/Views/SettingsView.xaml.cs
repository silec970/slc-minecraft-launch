using System.Windows.Controls;
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
}
