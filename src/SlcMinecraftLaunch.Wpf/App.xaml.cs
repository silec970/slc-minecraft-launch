using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ProjBobcat.Interface.Services;
using ProjBobcat.Services;
using SlcMinecraftLaunch.Application;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;
using SlcMinecraftLaunch.Infrastructure.Configuration;
using SlcMinecraftLaunch.Infrastructure.Crash;
using SlcMinecraftLaunch.Infrastructure.Java;
using SlcMinecraftLaunch.Infrastructure.Loaders;
using SlcMinecraftLaunch.Infrastructure.Mojang;
using SlcMinecraftLaunch.Infrastructure.Mods;
using SlcMinecraftLaunch.Infrastructure.Persistence;
using SlcMinecraftLaunch.Infrastructure.ProjBobcat;
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
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddInMemoryCollection(LauncherEndpointDefaults.Configuration)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddEnvironmentVariables("SLC_")
                .Build();

            var commandLine = CommandLineOptions.Parse(e.Args);
            var paths = PortableLauncherPaths.Resolve(
                commandLine.GameRoot ?? configuration["Game:RootDirectory"],
                AppContext.BaseDirectory);
            var manifestUrl = GetRequiredSetting(configuration, "Mojang:VersionManifestUrl");
            var javaSourceOptions = new JavaSourceOptions(
                GetRequiredSetting(configuration, "JavaSources:MojangRuntimeManifestUrl"),
                GetRequiredSetting(configuration, "JavaSources:AdoptiumAssetsUrlTemplate"));
            var loaderSourceOptions = new LoaderSourceOptions(
                GetRequiredSetting(configuration, "Loaders:FabricVersionsUrlTemplate"),
                GetRequiredSetting(configuration, "Loaders:FabricArtifactUrlTemplate"),
                GetRequiredSetting(configuration, "Loaders:ForgeMetadataUrl"),
                GetRequiredSetting(configuration, "Loaders:ForgeDownloadUrlTemplate"),
                GetRequiredSetting(configuration, "Loaders:NeoForgeMetadataUrl"),
                GetRequiredSetting(configuration, "Loaders:NeoForgeDownloadUrlTemplate"),
                GetRequiredSetting(configuration, "Loaders:OptiFineListUrlTemplate"),
                GetRequiredSetting(configuration, "Loaders:OptiFineDownloadUrlTemplate"));
            var downloadSourceOptions = new DownloadSourceOptions(
                GetConfiguredList(configuration, "DownloadSources:VersionMetadata"),
                GetConfiguredList(configuration, "DownloadSources:AssetIndex"),
                GetConfiguredList(configuration, "DownloadSources:AssetObjects"),
                GetConfiguredList(configuration, "DownloadSources:Libraries"),
                GetConfiguredList(configuration, "DownloadSources:ForgeFiles"),
                GetConfiguredList(configuration, "DownloadSources:ForgeMaven"),
                GetConfiguredList(configuration, "DownloadSources:FabricMaven"),
                GetConfiguredList(configuration, "DownloadSources:QuiltMaven"));
            var modApiOptions = new ModApiOptions(
                GetRequiredSetting(configuration, "ModApis:ModrinthBaseUrl"),
                GetRequiredSetting(configuration, "ModApis:CurseForgeBaseUrl"),
                Environment.GetEnvironmentVariable("CURSEFORGE_API_KEY"));

            var services = new ServiceCollection();
            services.AddHttpClient();
            services.AddSingleton(paths);
            services.AddSingleton(javaSourceOptions);
            services.AddSingleton(loaderSourceOptions);
            services.AddSingleton(downloadSourceOptions);
            services.AddSingleton(modApiOptions);
            services.AddSingleton<IJavaDiscovery, ProjBobcatJavaDiscovery>();
            services.AddSingleton<ProjBobcatRuntimeProvider>();
            services.AddSingleton<IGameRuntimeProvider>(
                serviceProvider => serviceProvider.GetRequiredService<ProjBobcatRuntimeProvider>());
            services.AddSingleton<IVersionPreferencesStore>(
                _ => new JsonVersionPreferencesStore(paths.DataRoot));
            services.AddSingleton<IUiSettingsStore>(
                _ => new JsonUiSettingsStore(paths.DataRoot));
            services.AddSingleton<IPlayerProfileStore>(
                _ => new JsonPlayerProfileStore(paths.DataRoot));
            services.AddSingleton<IGameVersionMaterializer>(serviceProvider =>
                new MojangVersionMaterializer(
                    serviceProvider.GetRequiredService<IHttpClientFactory>(),
                    manifestUrl));
            services.AddSingleton<IVanillaVersionCatalogService>(serviceProvider =>
                new MojangVersionCatalogService(
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
            services.AddSingleton<IVanillaInstallationService, VanillaInstallationService>();
            services.AddSingleton<ISmokeTestService, SmokeTestService>();
            services.AddSingleton<IVersionService, VersionService>();
            services.AddSingleton<ICrashAnalysisBackend, ProjBobcatCrashAnalysisBackend>();
            services.AddSingleton<ICrashReportStore>(
                _ => new JsonCrashReportStore(paths.DataRoot));
            services.AddSingleton<ICrashAnalysisService, CrashAnalysisService>();
            services.AddSingleton<CrashAnalysisCoordinator>();
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
                var client = serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient();
                client.DefaultRequestHeaders.UserAgent.ParseAdd("SlcMinecraftLaunch/0.1");
                if (!string.IsNullOrWhiteSpace(modApiOptions.CurseForgeApiKey))
                {
                    client.DefaultRequestHeaders.Add("x-api-key", modApiOptions.CurseForgeApiKey);
                }

                return new CurseForgeApiService(
                    client,
                    serviceProvider.GetRequiredService<ModApiSettingsProvider>());
            });
            services.AddSingleton<IModProviderCatalog>(serviceProvider =>
                new ProjBobcatModProviderCatalog(
                    serviceProvider.GetRequiredService<IModrinthApiService>(),
                    serviceProvider.GetRequiredService<ICurseForgeApiService>(),
                    !string.IsNullOrWhiteSpace(modApiOptions.CurseForgeApiKey)));
            services.AddSingleton<ILocalModStore, LocalModStore>();
            services.AddSingleton<ModConflictAnalyzer>();
            services.AddSingleton<IModService, ModService>();
            services.AddSingleton<IPlayerProfileService, PlayerProfileService>();
            services.AddSingleton<IGameLaunchService, GameLaunchService>();
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

            if (commandLine.AnalyzeCrash)
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                var crashService = _services.GetRequiredService<ICrashAnalysisService>();
                var crashReport = await crashService.AnalyzeAsync(
                    commandLine.Version
                    ?? configuration["SmokeTest:TargetVersion"]
                    ?? "1.21.4");
                await WriteCrashReportAsync(paths.DataRoot, crashReport).ConfigureAwait(true);
                Shutdown(0);
                return;
            }

            if (commandLine.SearchMods || commandLine.InstallMod || commandLine.ScanMods)
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                var modService = _services.GetRequiredService<IModService>();
                var versionId = commandLine.Version
                                ?? configuration["SmokeTest:TargetVersion"]
                                ?? "1.21.4";
                var loaderKind = Enum.TryParse<LoaderKind>(commandLine.Loader, true, out var parsedLoader)
                    ? parsedLoader
                    : LoaderKind.Vanilla;

                if (commandLine.SearchMods)
                {
                    var results = await modService.SearchAsync(new ModSearchQuery(
                        commandLine.Query ?? string.Empty,
                        versionId,
                        loaderKind));
                    await WriteModCommandResultAsync(
                        paths.DataRoot,
                        new { Kind = "search", Results = results }).ConfigureAwait(true);
                    Shutdown(0);
                    return;
                }

                if (commandLine.ScanMods)
                {
                    var mods = await modService.ScanAsync(versionId);
                    var conflicts = await modService.DetectConflictsAsync(versionId);
                    await WriteModCommandResultAsync(
                        paths.DataRoot,
                        new { Kind = "scan", Mods = mods, Conflicts = conflicts }).ConfigureAwait(true);
                    Shutdown(0);
                    return;
                }

                if (!Enum.TryParse<ModProvider>(commandLine.Provider, true, out var provider) ||
                    string.IsNullOrWhiteSpace(commandLine.ProjectId))
                {
                    await WriteModCommandResultAsync(
                        paths.DataRoot,
                        new ModInstallOutcome(false, "Mods.InvalidRequest")).ConfigureAwait(true);
                    Shutdown(1);
                    return;
                }

                var installResult = await modService.InstallRemoteAsync(
                    new ModInstallRequest(
                        provider,
                        commandLine.ProjectId,
                        versionId,
                        loaderKind));
                await WriteModCommandResultAsync(paths.DataRoot, installResult).ConfigureAwait(true);
                Shutdown(installResult.Succeeded ? 0 : 1);
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
        var values = configuration.GetSection(sectionPath)
            .GetChildren()
            .Select(child => child.Value)
            .OfType<string>()
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
        return values.Length == 0
            ? LauncherEndpointDefaults.GetDefaultList(sectionPath)
            : values;
    }

    private static string GetRequiredSetting(IConfiguration configuration, string key)
    {
        var configured = configuration[key];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        return LauncherEndpointDefaults.Configuration.GetValueOrDefault(key)
               ?? throw new InvalidOperationException($"{key} is not configured.");
    }

    private static async Task WriteCrashReportAsync(
        string dataRoot,
        CrashAnalysisReport report)
    {
        Directory.CreateDirectory(dataRoot);
        var outputPath = Path.Combine(dataRoot, "crash-analysis-result.json");
        var temporaryPath = $"{outputPath}.{Guid.NewGuid():N}.tmp";

        await using (var output = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(
                output,
                report,
                new JsonSerializerOptions { WriteIndented = true });
        }

        File.Move(temporaryPath, outputPath, true);
    }

    private static async Task WriteModCommandResultAsync(string dataRoot, object result)
    {
        Directory.CreateDirectory(dataRoot);
        var outputPath = Path.Combine(dataRoot, "mod-command-result.json");
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
}
