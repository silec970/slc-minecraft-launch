using Microsoft.Extensions.Configuration;
using SlcMinecraftLaunch.Infrastructure.Configuration;

namespace SlcMinecraftLaunch.Tests;

public sealed class LauncherEndpointDefaultsTests
{
    [Fact]
    public void Configuration_ProvidesEveryRequiredEndpoint()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(LauncherEndpointDefaults.Configuration)
            .Build();

        Assert.StartsWith("https://", configuration["Mojang:VersionManifestUrl"]);
        Assert.StartsWith("https://", configuration["JavaSources:MojangRuntimeManifestUrl"]);
        Assert.StartsWith("https://", configuration["Loaders:FabricArtifactUrlTemplate"]);
        Assert.StartsWith("https://", configuration["ModApis:ModrinthBaseUrl"]);
        Assert.NotEmpty(LauncherEndpointDefaults.GetDefaultList("DownloadSources:AssetObjects"));
        Assert.Equal("1.21.4", configuration["SmokeTest:TargetVersion"]);
    }
}
