namespace SlcMinecraftLaunch.Domain.Models;

public sealed record LoaderSourceOptions(
    string FabricVersionsUrlTemplate,
    string FabricArtifactUrlTemplate,
    string ForgeMetadataUrl,
    string ForgeDownloadUrlTemplate,
    string NeoForgeMetadataUrl,
    string NeoForgeDownloadUrlTemplate,
    string OptiFineListUrlTemplate,
    string OptiFineDownloadUrlTemplate);

