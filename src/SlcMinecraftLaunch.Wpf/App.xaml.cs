using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using ProjBobcat.Interface.Services;
using ProjBobcat.Services;
using SlcMinecraftLaunch.Application;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;
using SlcMinecraftLaunch.Infrastructure.Configuration;
using SlcMinecraftLaunch.Infrastructure.Crash;
using SlcMinecraftLaunch.Infrastructure.Java;
using SlcMinecraftLaunch.Infrastructure.Instances;
using SlcMinecraftLaunch.Infrastructure.Loaders;
using SlcMinecraftLaunch.Infrastructure.Logging;
using SlcMinecraftLaunch.Infrastructure.Mojang;
using SlcMinecraftLaunch.Infrastructure.Mods;
using SlcMinecraftLaunch.Infrastructure.Persistence;
using SlcMinecraftLaunch.Infrastructure.ProjBobcat;
using SlcMinecraftLaunch.Infrastructure.Security;
using SlcMinecraftLaunch.Wpf.ViewModels;
using SlcMinecraftLaunch.Wpf.Services;

namespace SlcMinecraftLaunch.Wpf;

public partial class App : System.Windows.Application
{
    private ServiceProvider? _services;

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
            var commandLine = CommandLineOptions.Parse(e.Args);
            var runtimeConfiguration = LauncherRuntimeConfigurationFactory.Load(
                AppContext.BaseDirectory,
                commandLine.GameRoot);
            var paths = runtimeConfiguration.Paths;
            var javaSourceOptions = runtimeConfiguration.JavaSources;
            var loaderSourceOptions = runtimeConfiguration.Loaders;
            var downloadSourceOptions = runtimeConfiguration.DownloadSources;
            var modApiOptions = runtimeConfiguration.ModApis;

            var services = new ServiceCollection();
            services.AddHttpClient();
            services.AddSingleton(runtimeConfiguration);
            services.AddSingleton(paths);
            services.AddSingleton(javaSourceOptions);
            services.AddSingleton(loaderSourceOptions);
            services.AddSingleton(downloadSourceOptions);
            services.AddSingleton(modApiOptions);
            services.AddSingleton<JsonSourceHealthStore>(
                _ => new JsonSourceHealthStore(paths.DataRoot));
            services.AddSingleton<ISourceHealthService, DownloadSourceHealthService>();
            services.AddSingleton<IDownloadTaskStore>(
                _ => new JsonDownloadTaskStore(paths.DataRoot));
            services.AddSingleton<ProjBobcatRuntimeProvider>();
            services.AddSingleton<IGameRuntimeProvider>(
                serviceProvider => serviceProvider.GetRequiredService<ProjBobcatRuntimeProvider>());
            services.AddSingleton<IVersionPreferencesStore>(
                _ => new JsonVersionPreferencesStore(paths.DataRoot));
            services.AddSingleton<IUiSettingsStore>(
                _ => new JsonUiSettingsStore(paths.DataRoot));
            services.AddSingleton<IPlayerProfileStore>(
                _ => new JsonPlayerProfileStore(paths.DataRoot));
            services.AddSingleton<IJavaSettingsStore>(
                _ => new JsonJavaSettingsStore(paths.DataRoot));
            services.AddSingleton<JavaRuntimeIntegrityVerifier>();
            services.AddSingleton<ManagedJavaRuntimeStore>();
            services.AddSingleton<IJavaRuntimeStore>(serviceProvider =>
                serviceProvider.GetRequiredService<ManagedJavaRuntimeStore>());
            services.AddSingleton<ProjBobcatJavaDiscovery>();
            services.AddSingleton<IJavaDiscovery>(serviceProvider =>
                serviceProvider.GetRequiredService<ProjBobcatJavaDiscovery>());
            services.AddSingleton<IGameVersionMaterializer>(serviceProvider =>
                new MojangVersionMaterializer(
                    serviceProvider.GetRequiredService<IHttpClientFactory>(),
                    runtimeConfiguration.MojangVersionManifestUrl));
            services.AddSingleton<IVanillaVersionCatalogService>(serviceProvider =>
                new MojangVersionCatalogService(
                    serviceProvider.GetRequiredService<IHttpClientFactory>(),
                    runtimeConfiguration.MojangVersionManifestUrl));
            services.AddSingleton<IJavaRuntimeProvisioner>(serviceProvider =>
                new JavaRuntimeProvisioner(
                    paths,
                    serviceProvider.GetRequiredService<IHttpClientFactory>(),
                    javaSourceOptions.MojangRuntimeManifestUrl,
                    javaSourceOptions.AdoptiumAssetsUrlTemplate));
            services.AddSingleton<JavaSelectionPolicy>();
            services.AddSingleton<JavaRuntimeService>();
            services.AddSingleton<IJavaRuntimeService>(serviceProvider =>
                serviceProvider.GetRequiredService<JavaRuntimeService>());
            services.AddSingleton<IJavaRuntimeManagementService>(serviceProvider =>
                serviceProvider.GetRequiredService<JavaRuntimeService>());
            services.AddSingleton<ILoaderCatalogService, ProjBobcatLoaderCatalogService>();
            services.AddSingleton<RemoteArtifactHashVerifier>();
            services.AddSingleton<ILoaderInstallBackend>(serviceProvider =>
                new ProjBobcatLoaderInstallBackend(
                    paths,
                    serviceProvider.GetRequiredService<IHttpClientFactory>(),
                    serviceProvider.GetRequiredService<ProjBobcatRuntimeProvider>(),
                    loaderSourceOptions,
                    serviceProvider.GetRequiredService<RemoteArtifactHashVerifier>()));
            services.AddSingleton<ILoaderInstallationService, LoaderInstallationService>();
            services.AddSingleton<IDownloadTaskService, DownloadTaskService>();
            services.AddSingleton<IDownloadService, DownloadService>();
            services.AddSingleton<IVanillaInstallationService, VanillaInstallationService>();
            services.AddSingleton<ISmokeTestService, SmokeTestService>();
            services.AddSingleton<IVersionService, VersionService>();
            services.AddSingleton<IInstanceStorage, LocalInstanceStorage>();
            services.AddSingleton<IInstanceService, InstanceService>();
            services.AddSingleton<IInstanceTransferService, InstanceTransferService>();
            services.AddSingleton<ICrashAnalysisBackend, ProjBobcatCrashAnalysisBackend>();
            services.AddSingleton<ICrashReportStore>(
                _ => new JsonCrashReportStore(paths.DataRoot));
            services.AddSingleton<IAppLogger, DebugAppLogger>();
            services.AddSingleton<CrashAnalysisService>();
            services.AddSingleton<ICrashAnalysisService>(serviceProvider =>
                serviceProvider.GetRequiredService<CrashAnalysisService>());
            services.AddSingleton<ICrashHistoryService>(serviceProvider =>
                serviceProvider.GetRequiredService<CrashAnalysisService>());
            services.AddSingleton<CrashAnalysisCoordinator>();
            services.AddSingleton<TokenProtector>();
            services.AddSingleton(serviceProvider => new JsonCurseForgeCredentialStore(
                paths.DataRoot,
                serviceProvider.GetRequiredService<TokenProtector>()));
            services.AddSingleton<ModApiCredentialState>();
            services.AddSingleton<CurseForgeCredentialService>();
            services.AddSingleton<ICurseForgeCredentialService>(
                serviceProvider => serviceProvider.GetRequiredService<CurseForgeCredentialService>());
            services.AddTransient<CurseForgeAuthenticationHandler>();
            services.AddHttpClient("CurseForgeApi")
                .AddHttpMessageHandler<CurseForgeAuthenticationHandler>();
            services.AddSingleton<ModApiSettingsProvider>();
            services.AddSingleton<IModrinthApiService>(serviceProvider =>
            {
                var client = serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("SlcMinecraftLaunch/0.1");
                return new ModrinthApiService(
                    client,
                    serviceProvider.GetRequiredService<ModApiSettingsProvider>());
            });
            services.AddSingleton<ICurseForgeApiService>(serviceProvider =>
            {
                var client = serviceProvider
                    .GetRequiredService<IHttpClientFactory>()
                    .CreateClient("CurseForgeApi");
                client.DefaultRequestHeaders.UserAgent.ParseAdd("SlcMinecraftLaunch/0.1");

                return new CurseForgeApiService(
                    client,
                    serviceProvider.GetRequiredService<ModApiSettingsProvider>());
            });
            services.AddSingleton<IModProviderCatalog>(serviceProvider =>
                new ProjBobcatModProviderCatalog(
                    serviceProvider.GetRequiredService<IModrinthApiService>(),
                    serviceProvider.GetRequiredService<ICurseForgeApiService>(),
                    serviceProvider.GetRequiredService<ICurseForgeCredentialService>()));
            services.AddSingleton<ILocalModStore, LocalModStore>();
            services.AddSingleton<IModSourceStore>(
                _ => new JsonModSourceStore(paths.DataRoot));
            services.AddSingleton<ModConflictAnalyzer>();
            services.AddSingleton<IModService, ModService>();
            services.AddSingleton<IPlayerProfileService, PlayerProfileService>();
            services.AddSingleton<IGameLaunchService, GameLaunchService>();
            services.AddSingleton<ICommandResultWriter, JsonCommandResultWriter>();
            services.AddSingleton<CommandLineExecutionService>();
            services.AddSingleton<BackgroundMusicService>();
            services.AddSingleton<UiPreferencesService>();
            services.AddSingleton<IUiPreferences>(
                serviceProvider => serviceProvider.GetRequiredService<UiPreferencesService>());
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<LaunchApplicationService>();
            services.AddSingleton<HomeViewModel>();
            services.AddSingleton<VersionsViewModel>();
            services.AddSingleton<DownloadLoaderViewModel>();
            services.AddSingleton<JavaViewModel>();
            services.AddSingleton<ModsViewModel>();
            services.AddSingleton<CrashViewModel>();
            services.AddSingleton<SettingsViewModel>();
            services.AddSingleton<ShellViewModel>();

            _services = services.BuildServiceProvider();
            await _services
                .GetRequiredService<ICurseForgeCredentialService>()
                .LoadStatusAsync()
                .ConfigureAwait(true);

            if (CommandLineExecutionService.IsHeadlessCommand(commandLine))
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                var result = await _services
                    .GetRequiredService<CommandLineExecutionService>()
                    .RunAsync(commandLine)
                    .ConfigureAwait(true);
                Shutdown(result.ExitCode);
                return;
            }

            ShutdownMode = ShutdownMode.OnMainWindowClose;
            var settingsService = _services.GetRequiredService<ISettingsService>();
            await _services.GetRequiredService<IUiPreferences>().ApplyAsync(
                await settingsService.LoadUiSettingsAsync());
            var runtime = await _services.GetRequiredService<IGameRuntimeProvider>().GetAsync();
            _services.GetRequiredService<CrashAnalysisCoordinator>().Start(runtime);
            var viewModel = _services.GetRequiredService<ShellViewModel>();
            await viewModel.InitializeAsync();

            var window = new MainWindow
            {
                DataContext = viewModel
            };

            MainWindow = window;
            window.Show();
        }
        catch (Exception exception)
        {
            try
            {
                var errorDirectory = Path.Combine(AppContext.BaseDirectory, "launcher-data");
                Directory.CreateDirectory(errorDirectory);
                await File.WriteAllTextAsync(
                    Path.Combine(errorDirectory, "startup-error.log"),
                    exception.ToString());
            }
            catch
            {
                // The original startup exception remains the primary failure.
            }

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

        base.OnExit(e);
    }

}
