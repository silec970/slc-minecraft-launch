using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SlcMinecraftLaunch.Application;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;
using SlcMinecraftLaunch.Infrastructure.Configuration;
using SlcMinecraftLaunch.Infrastructure.Java;
using SlcMinecraftLaunch.Infrastructure.Loaders;
using SlcMinecraftLaunch.Infrastructure.Mojang;
using SlcMinecraftLaunch.Infrastructure.Persistence;
using SlcMinecraftLaunch.Infrastructure.ProjBobcat;
using SlcMinecraftLaunch.Wpf.ViewModels;

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
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddEnvironmentVariables("SLC_")
                .Build();

            var commandLine = CommandLineOptions.Parse(e.Args);
            var paths = PortableLauncherPaths.Resolve(
                commandLine.GameRoot ?? configuration["Game:RootDirectory"],
                AppContext.BaseDirectory);
            var manifestUrl = configuration["Mojang:VersionManifestUrl"]
                              ?? throw new InvalidOperationException(
                                  "Mojang:VersionManifestUrl is not configured.");
            var javaSourceOptions = new JavaSourceOptions(
                configuration["JavaSources:MojangRuntimeManifestUrl"]
                ?? throw new InvalidOperationException("JavaSources:MojangRuntimeManifestUrl is not configured."),
                configuration["JavaSources:AdoptiumAssetsUrlTemplate"]
                ?? throw new InvalidOperationException("JavaSources:AdoptiumAssetsUrlTemplate is not configured."));
            var loaderSourceOptions = new LoaderSourceOptions(
                configuration["Loaders:FabricVersionsUrlTemplate"]
                ?? throw new InvalidOperationException("Loaders:FabricVersionsUrlTemplate is not configured."),
                configuration["Loaders:FabricArtifactUrlTemplate"]
                ?? throw new InvalidOperationException("Loaders:FabricArtifactUrlTemplate is not configured."),
                configuration["Loaders:ForgeMetadataUrl"]
                ?? throw new InvalidOperationException("Loaders:ForgeMetadataUrl is not configured."),
                configuration["Loaders:ForgeDownloadUrlTemplate"]
                ?? throw new InvalidOperationException("Loaders:ForgeDownloadUrlTemplate is not configured."),
                configuration["Loaders:NeoForgeMetadataUrl"]
                ?? throw new InvalidOperationException("Loaders:NeoForgeMetadataUrl is not configured."),
                configuration["Loaders:NeoForgeDownloadUrlTemplate"]
                ?? throw new InvalidOperationException("Loaders:NeoForgeDownloadUrlTemplate is not configured."),
                configuration["Loaders:OptiFineListUrlTemplate"]
                ?? throw new InvalidOperationException("Loaders:OptiFineListUrlTemplate is not configured."),
                configuration["Loaders:OptiFineDownloadUrlTemplate"]
                ?? throw new InvalidOperationException("Loaders:OptiFineDownloadUrlTemplate is not configured."));
            var downloadSourceOptions = new DownloadSourceOptions(
                GetConfiguredList(configuration, "DownloadSources:VersionMetadata"),
                GetConfiguredList(configuration, "DownloadSources:AssetIndex"),
                GetConfiguredList(configuration, "DownloadSources:AssetObjects"),
                GetConfiguredList(configuration, "DownloadSources:Libraries"),
                GetConfiguredList(configuration, "DownloadSources:ForgeFiles"),
                GetConfiguredList(configuration, "DownloadSources:ForgeMaven"),
                GetConfiguredList(configuration, "DownloadSources:FabricMaven"),
                GetConfiguredList(configuration, "DownloadSources:QuiltMaven"));

            var services = new ServiceCollection();
            services.AddHttpClient();
            services.AddSingleton(paths);
            services.AddSingleton(javaSourceOptions);
            services.AddSingleton(loaderSourceOptions);
            services.AddSingleton(downloadSourceOptions);
            services.AddSingleton<IJavaDiscovery, ProjBobcatJavaDiscovery>();
            services.AddSingleton<ProjBobcatRuntimeProvider>();
            services.AddSingleton<IGameRuntimeProvider>(
                serviceProvider => serviceProvider.GetRequiredService<ProjBobcatRuntimeProvider>());
            services.AddSingleton<IVersionPreferencesStore>(
                _ => new JsonVersionPreferencesStore(paths.DataRoot));
            services.AddSingleton<IGameVersionMaterializer>(serviceProvider =>
                new MojangVersionMaterializer(
                    serviceProvider.GetRequiredService<IHttpClientFactory>(),
                    manifestUrl));
            services.AddSingleton<IJavaRuntimeProvisioner>(serviceProvider =>
                new JavaRuntimeProvisioner(
                    paths,
                    serviceProvider.GetRequiredService<IHttpClientFactory>(),
                    javaSourceOptions.MojangRuntimeManifestUrl,
                    javaSourceOptions.AdoptiumAssetsUrlTemplate));
            services.AddSingleton<JavaSelectionPolicy>();
            services.AddSingleton<IJavaRuntimeService, JavaRuntimeService>();
            services.AddSingleton<ILoaderCatalogService, ProjBobcatLoaderCatalogService>();
            services.AddSingleton<ILoaderInstallBackend>(serviceProvider =>
                new ProjBobcatLoaderInstallBackend(
                    paths,
                    serviceProvider.GetRequiredService<IHttpClientFactory>(),
                    serviceProvider.GetRequiredService<ProjBobcatRuntimeProvider>(),
                    loaderSourceOptions));
            services.AddSingleton<ILoaderInstallationService, LoaderInstallationService>();
            services.AddSingleton<IDownloadService, DownloadService>();
            services.AddSingleton<ISmokeTestService, SmokeTestService>();
            services.AddSingleton<IVersionService, VersionService>();
            services.AddSingleton<LaunchApplicationService>();
            services.AddSingleton<ShellViewModel>();

            _services = services.BuildServiceProvider();

            if (commandLine.ListVersions)
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                var versionService = _services.GetRequiredService<IVersionService>();
                var catalog = await versionService.GetCatalogAsync();
                await WriteVersionCatalogAsync(paths.DataRoot, catalog).ConfigureAwait(true);
                Shutdown(0);
                return;
            }

            if (commandLine.ProvisionJava)
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                if (commandLine.JavaMajor is not { } javaMajor)
                {
                    await WriteJavaProvisionResultAsync(
                        paths.DataRoot,
                        new JavaRuntimeProvisionOutcome(
                            false,
                            null,
                            "Java.InvalidMajor")).ConfigureAwait(true);
                    Shutdown(1);
                    return;
                }

                var component = configuration[$"JavaSources:MojangComponents:{javaMajor}"];
                var provisioner = _services.GetRequiredService<IJavaRuntimeProvisioner>();
                var provisionResult = await provisioner.EnsureAsync(javaMajor, component);
                await WriteJavaProvisionResultAsync(paths.DataRoot, provisionResult).ConfigureAwait(true);
                Shutdown(provisionResult.Succeeded ? 0 : 1);
                return;
            }

            if (commandLine.InstallLoader)
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                if (!Enum.TryParse<LoaderKind>(commandLine.Loader, true, out var loaderKind))
                {
                    await WriteLoaderResultAsync(
                        paths.DataRoot,
                        new LoaderInstallOutcome(false, "Loader.InvalidKind", ErrorDetail: commandLine.Loader))
                        .ConfigureAwait(true);
                    Shutdown(1);
                    return;
                }

                var loaderService = _services.GetRequiredService<ILoaderInstallationService>();
                var loaderResult = await loaderService.InstallAsync(
                    new LoaderInstallRequest(
                        commandLine.Version
                        ?? configuration["SmokeTest:TargetVersion"]
                        ?? "1.21.4",
                        loaderKind,
                        commandLine.LoaderVersion,
                        commandLine.JavaPath));
                await WriteLoaderResultAsync(paths.DataRoot, loaderResult).ConfigureAwait(true);
                Shutdown(loaderResult.Succeeded ? 0 : 1);
                return;
            }

            if (commandLine.SmokeTest)
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;

                var targetVersion = commandLine.Version
                                    ?? configuration["SmokeTest:TargetVersion"]
                                    ?? "1.21.4";
                var smokeService = _services.GetRequiredService<ISmokeTestService>();
                var result = await smokeService.RunAsync(
                    new SmokeTestRequest(
                        paths.GameRoot,
                        targetVersion,
                        commandLine.JavaPath));

                await WriteSmokeResultAsync(paths.DataRoot, result).ConfigureAwait(true);
                Shutdown(result.Succeeded ? 0 : 1);
                return;
            }

            ShutdownMode = ShutdownMode.OnMainWindowClose;
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

        base.OnExit(e);
    }

    private static async Task WriteSmokeResultAsync(string dataRoot, SmokeTestResult result)
    {
        Directory.CreateDirectory(dataRoot);
        var outputPath = Path.Combine(dataRoot, "smoke-result.json");
        var temporaryPath = $"{outputPath}.{Guid.NewGuid():N}.tmp";

        await using (var output = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(
                output,
                new
                {
                    result.Succeeded,
                    result.TargetVersion,
                    result.FinalStage,
                    result.MessageKey,
                    result.Duration,
                    result.ExitCode,
                    result.Warnings,
                    result.ErrorDetail,
                    ProgressCount = result.Progress.Count,
                    LastProgress = result.Progress.LastOrDefault()
                },
                new JsonSerializerOptions { WriteIndented = true });
        }

        File.Move(temporaryPath, outputPath, true);
    }

    private static async Task WriteVersionCatalogAsync(string dataRoot, GameVersionCatalog catalog)
    {
        Directory.CreateDirectory(dataRoot);
        var outputPath = Path.Combine(dataRoot, "version-catalog.json");
        var temporaryPath = $"{outputPath}.{Guid.NewGuid():N}.tmp";

        await using (var output = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(
                output,
                catalog,
                new JsonSerializerOptions { WriteIndented = true });
        }

        File.Move(temporaryPath, outputPath, true);
    }

    private static async Task WriteLoaderResultAsync(string dataRoot, LoaderInstallOutcome result)
    {
        Directory.CreateDirectory(dataRoot);
        var outputPath = Path.Combine(dataRoot, "loader-install-result.json");
        var temporaryPath = $"{outputPath}.{Guid.NewGuid():N}.tmp";

        await using (var output = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(
                output,
                result,
                new JsonSerializerOptions { WriteIndented = true });
        }

        File.Move(temporaryPath, outputPath, true);
    }

    private static async Task WriteJavaProvisionResultAsync(
        string dataRoot,
        JavaRuntimeProvisionOutcome result)
    {
        Directory.CreateDirectory(dataRoot);
        var outputPath = Path.Combine(dataRoot, "java-provision-result.json");
        var temporaryPath = $"{outputPath}.{Guid.NewGuid():N}.tmp";

        await using (var output = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(
                output,
                result,
                new JsonSerializerOptions { WriteIndented = true });
        }

        File.Move(temporaryPath, outputPath, true);
    }

    private static IReadOnlyList<string> GetConfiguredList(
        IConfiguration configuration,
        string sectionPath)
    {
        return configuration.GetSection(sectionPath)
            .GetChildren()
            .Select(child => child.Value)
            .OfType<string>()
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
    }
}
