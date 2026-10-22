namespace SlcMinecraftLaunch.Domain.Models;

public sealed record JavaSourceOptions(
    string MojangRuntimeManifestUrl,
    string AdoptiumAssetsUrlTemplate);

