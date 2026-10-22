namespace SlcMinecraftLaunch.Infrastructure.Configuration;

public static class LauncherEndpointDefaults
{
    public const string VersionManifestUrl =
        "https://launchermeta.mojang.com/mc/game/version_manifest_v2.json";

    public const string MojangRuntimeManifestUrl =
        "https://launchermeta.mojang.com/v1/products/java-runtime/2ec0cc96c44e5a76b9c8b7c39df7210883d12871/all.json";

    public const string AdoptiumAssetsUrlTemplate =
        "https://api.adoptium.net/v3/assets/latest/{major}/hotspot?architecture={arch}&image_type=jdk&os=windows&vendor=eclipse";

    public const string FabricVersionsUrlTemplate =
        "https://meta.fabricmc.net/v2/versions/loader/{minecraft}";

    public const string FabricArtifactUrlTemplate =
        "https://meta.fabricmc.net/v2/versions/loader/{minecraft}/{loader}";

    public const string ForgeMetadataUrl =
        "https://maven.minecraftforge.net/net/minecraftforge/forge/maven-metadata.xml";

    public const string ForgeDownloadUrlTemplate =
        "https://maven.minecraftforge.net/net/minecraftforge/forge/{full}/forge-{full}-installer.jar";

    public const string NeoForgeMetadataUrl =
        "https://maven.neoforged.net/releases/net/neoforged/neoforge/maven-metadata.xml";

    public const string NeoForgeDownloadUrlTemplate =
        "https://maven.neoforged.net/releases/net/neoforged/neoforge/{loader}/neoforge-{loader}-installer.jar";

    public const string OptiFineListUrlTemplate =
        "https://bmclapi2.bangbang93.com/optifine/{minecraft}";

    public const string OptiFineDownloadUrlTemplate =
        "https://bmclapi2.bangbang93.com/optifine/{minecraft}/{type}/{patch}";

    public const string ModrinthBaseUrl = "https://api.modrinth.com/v2";
    public const string CurseForgeBaseUrl = "https://api.curseforge.com/v1";

    public static IReadOnlyDictionary<string, string?> Configuration { get; } =
        BuildConfiguration();

    public static IReadOnlyList<string> GetDefaultList(string sectionPath)
    {
        var prefix = $"{sectionPath}:";
        return Configuration
            .Where(pair => pair.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => pair.Value)
            .OfType<string>()
            .ToArray();
    }

    private static IReadOnlyDictionary<string, string?> BuildConfiguration()
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Game:RootDirectory"] = string.Empty,
            ["Mojang:VersionManifestUrl"] = VersionManifestUrl,
            ["JavaSources:MojangRuntimeManifestUrl"] = MojangRuntimeManifestUrl,
            ["JavaSources:AdoptiumAssetsUrlTemplate"] = AdoptiumAssetsUrlTemplate,
            ["JavaSources:MojangComponents:8"] = "jre-legacy",
            ["JavaSources:MojangComponents:17"] = "java-runtime-gamma",
            ["JavaSources:MojangComponents:21"] = "java-runtime-delta",
            ["JavaSources:MojangComponents:25"] = "java-runtime-epsilon",
            ["Loaders:FabricVersionsUrlTemplate"] = FabricVersionsUrlTemplate,
            ["Loaders:FabricArtifactUrlTemplate"] = FabricArtifactUrlTemplate,
            ["Loaders:ForgeMetadataUrl"] = ForgeMetadataUrl,
            ["Loaders:ForgeDownloadUrlTemplate"] = ForgeDownloadUrlTemplate,
            ["Loaders:NeoForgeMetadataUrl"] = NeoForgeMetadataUrl,
            ["Loaders:NeoForgeDownloadUrlTemplate"] = NeoForgeDownloadUrlTemplate,
            ["Loaders:OptiFineListUrlTemplate"] = OptiFineListUrlTemplate,
            ["Loaders:OptiFineDownloadUrlTemplate"] = OptiFineDownloadUrlTemplate,
            ["ModApis:ModrinthBaseUrl"] = ModrinthBaseUrl,
            ["ModApis:CurseForgeBaseUrl"] = CurseForgeBaseUrl,
            ["SmokeTest:TargetVersion"] = "1.21.4"
        };

        AddList(values, "DownloadSources:VersionMetadata", "https://piston-meta.mojang.com/");
        AddList(values, "DownloadSources:AssetIndex", "https://launchermeta.mojang.com/");
        AddList(values, "DownloadSources:AssetObjects", "https://resources.download.minecraft.net/");
        AddList(values, "DownloadSources:Libraries", "https://libraries.minecraft.net/");
        AddList(values, "DownloadSources:ForgeFiles", "https://files.minecraftforge.net/");
        AddList(values, "DownloadSources:ForgeMaven", "https://maven.minecraftforge.net/");
        AddList(values, "DownloadSources:FabricMaven", "https://maven.fabricmc.net/");
        AddList(values, "DownloadSources:QuiltMaven", "https://maven.quiltmc.org/repository/release/");
        return values;
    }

    private static void AddList(
        IDictionary<string, string?> values,
        string key,
        string value)
    {
        values[$"{key}:0"] = value;
    }
}
