using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;
using SlcMinecraftLaunch.Wpf.ViewModels;

namespace SlcMinecraftLaunch.Tests;

public sealed class ViewModelContractTests
{
    [Fact]
    public void PageViewModels_ExposeLocalizationKeys()
    {
        PageViewModel[] pages =
        [
            new HomeViewModel(null!, null!),
            new VersionsViewModel(null!),
            new DownloadLoaderViewModel(null!, null!, null!, null!, null!),
            new JavaViewModel(null!, null!),
            new ModsViewModel(null!, null!),
            new CrashViewModel(null!, null!),
            new SettingsViewModel(new FakeSettingsService())
        ];

        Assert.All(pages, page =>
        {
            Assert.Matches("^[A-Za-z.]+$", page.TitleKey);
            Assert.Matches("^[A-Za-z.]+$", page.DescriptionKey);
            Assert.Equal("Status.Ready", page.StatusKey);
        });
    }

    [Fact]
    public void Shell_ContainsOnePagePerWorkflowModule()
    {
        var home = new HomeViewModel(null!, null!);
        var versions = new VersionsViewModel(null!);
        var download = new DownloadLoaderViewModel(null!, null!, null!, null!, null!);
        var java = new JavaViewModel(null!, null!);
        var mods = new ModsViewModel(null!, null!);
        var crash = new CrashViewModel(null!, null!);
        var settings = new SettingsViewModel(new FakeSettingsService());

        var shell = new ShellViewModel(home, versions, download, java, mods, crash, settings);

        Assert.Equal(7, shell.NavigationItems.Count);
        Assert.Same(home, shell.CurrentPage);
        Assert.Contains(shell.NavigationItems, item => item.Page is ModsViewModel);
        Assert.Contains(shell.NavigationItems, item => item.Page is CrashViewModel);
    }

    private sealed class FakeSettingsService : ISettingsService
    {
        public LauncherSettingsSnapshot GetSnapshot()
            => new(@"C:\Minecraft", @"C:\Data", "test");
    }
}
