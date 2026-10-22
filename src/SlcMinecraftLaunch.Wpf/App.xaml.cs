using System.Net;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SlcMinecraftLaunch.Application;
using SlcMinecraftLaunch.Infrastructure.Configuration;
using SlcMinecraftLaunch.Infrastructure.ProjBobcat;
using SlcMinecraftLaunch.Wpf.ViewModels;

namespace SlcMinecraftLaunch.Wpf;

public partial class App : System.Windows.Application
{
    private ServiceProvider? _services;
    private ProjBobcatGameRuntime? _gameRuntime;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // PROJBOBCAT: ProjBobcat 2.1.2 requires the connection limit to be
        // raised before any DownloadHelper-backed operation.
#pragma warning disable SYSLIB0014 // Required by the ProjBobcat 2.1.2 download contract.
        ServicePointManager.DefaultConnectionLimit = 512;
#pragma warning restore SYSLIB0014

        try
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddEnvironmentVariables("SLC_")
                .Build();

            var paths = PortableLauncherPaths.Resolve(
                configuration["Game:RootDirectory"],
                AppContext.BaseDirectory);

            _gameRuntime = await ProjBobcatGameRuntime.CreateAsync(paths);

            var services = new ServiceCollection();
            services.AddSingleton(_gameRuntime);
            services.AddSingleton<LaunchApplicationService>();
            services.AddSingleton<ShellViewModel>();

            _services = services.BuildServiceProvider();

            var viewModel = _services.GetRequiredService<ShellViewModel>();
            await viewModel.RefreshAsync();

            var window = new MainWindow
            {
                DataContext = viewModel
            };

            MainWindow = window;
            window.Show();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                exception.ToString(),
                FindResource("App.Title")?.ToString() ?? "SLC Minecraft Launcher",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_services is not null)
        {
            await _services.DisposeAsync();
        }
        else if (_gameRuntime is not null)
        {
            await _gameRuntime.DisposeAsync();
        }

        base.OnExit(e);
    }
}

