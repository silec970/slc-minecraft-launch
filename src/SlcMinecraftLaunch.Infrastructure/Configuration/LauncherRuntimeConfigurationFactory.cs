using Microsoft.Extensions.Configuration;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Infrastructure.Configuration;

public static class LauncherRuntimeConfigurationFactory
{
    public static LauncherRuntimeConfiguration Load(
        string applicationDirectory,
        string? gameRootOverride)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(applicationDirectory)
            .AddInMemoryCollection(LauncherEndpointDefaults.Configuration)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables("SLC_")
            .Build();

        var paths = PortableLauncherPaths.Resolve(
            gameRootOverride ?? configuration["Game:RootDirectory"],
            applicationDirectory);
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
            GetRequiredSetting(configuration, "ModApis:CurseForgeBaseUrl"));
        var javaComponents = new Dictionary<int, string>();
        foreach (var major in new[] { 8, 17, 21, 25 })
        {
            var component = configuration[$"JavaSources:MojangComponents:{major}"];
            if (!string.IsNullOrWhiteSpace(component))
            {
                javaComponents[major] = component;
            }
        }

        return new LauncherRuntimeConfiguration(
            paths,
            GetRequiredSetting(configuration, "Mojang:VersionManifestUrl"),
            javaSourceOptions,
            loaderSourceOptions,
            downloadSourceOptions,
            modApiOptions,
            javaComponents,
            configuration["SmokeTest:TargetVersion"] ?? "1.21.4");
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
}
