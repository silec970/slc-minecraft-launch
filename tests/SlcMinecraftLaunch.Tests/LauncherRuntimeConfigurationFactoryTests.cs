using SlcMinecraftLaunch.Infrastructure.Configuration;

namespace SlcMinecraftLaunch.Tests;

public sealed class LauncherRuntimeConfigurationFactoryTests
{
    [Fact]
    public void Load_WithoutAppSettings_UsesCodeDefaultsAndPortableDirectories()
    {
        var root = Path.Combine(Path.GetTempPath(), $"slc-runtime-config-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var configuration = LauncherRuntimeConfigurationFactory.Load(root, null);

            Assert.Equal(
                Path.Combine(Path.GetFullPath(root), ".minecraft"),
                configuration.Paths.GameRoot);
            Assert.Equal(
                Path.Combine(Path.GetFullPath(root), "launcher-data"),
                configuration.Paths.DataRoot);
            Assert.Equal(
                LauncherEndpointDefaults.VersionManifestUrl,
                configuration.MojangVersionManifestUrl);
            Assert.NotEmpty(configuration.DownloadSources.AssetObjectRoots);
            Assert.Equal("java-runtime-delta", configuration.MojangJavaComponents[21]);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
