using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Infrastructure.Configuration;

public sealed record LauncherRuntimeConfiguration(
    PortableLauncherPaths Paths,
    string MojangVersionManifestUrl,
    JavaSourceOptions JavaSources,
    LoaderSourceOptions Loaders,
    DownloadSourceOptions DownloadSources,
    ModApiOptions ModApis,
    IReadOnlyDictionary<int, string> MojangJavaComponents,
    string DefaultTargetVersion);
