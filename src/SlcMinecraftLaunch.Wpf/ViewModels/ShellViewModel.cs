using CommunityToolkit.Mvvm.ComponentModel;
using SlcMinecraftLaunch.Application;

namespace SlcMinecraftLaunch.Wpf.ViewModels;

public sealed partial class ShellViewModel(LaunchApplicationService applicationService) : ObservableObject
{
    [ObservableProperty]
    private int _installedVersionCount;

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await applicationService.GetSnapshotAsync(cancellationToken);
        InstalledVersionCount = snapshot.VersionIds.Count;
    }
}

