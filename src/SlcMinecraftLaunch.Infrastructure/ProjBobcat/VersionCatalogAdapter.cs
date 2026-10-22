using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Interface;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Infrastructure.ProjBobcat;

internal sealed class VersionCatalogAdapter(IVersionLocator versionLocator)
{
    public Task<GameRuntimeSnapshot> GetSnapshotAsync(
        string rootPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // PROJBOBCAT: IVersionLocator.GetAllGames is the only supported source
        // for the installed version list.
        var versions = versionLocator.GetAllGames()
            .Select(version => version switch
            {
                VersionInfo concreteVersion => concreteVersion.Id,
                _ => version.DirName
            })
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Task.FromResult(new GameRuntimeSnapshot(rootPath, versions, true));
    }

    public Task<IReadOnlyList<GameVersionDescriptor>> GetVersionsAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // PROJBOBCAT: version discovery always starts from the locator's
        // GetAllGames result; no version directory JSON is read here.
        var versions = versionLocator.GetAllGames()
            .Select(version => version switch
            {
                VersionInfo concrete => CreateVersionDescriptor(concrete),
                _ => new GameVersionDescriptor(
                    version.DirName,
                    string.IsNullOrWhiteSpace(version.Name) ? version.DirName : version.Name,
                    version.DirName,
                    LoaderKind.Other,
                    null,
                    null,
                    null,
                    true)
            })
            .OrderBy(version => version.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Task.FromResult<IReadOnlyList<GameVersionDescriptor>>(versions);
    }

    private static GameVersionDescriptor CreateVersionDescriptor(VersionInfo version)
    {
        var rawVersion = version.RawVersion;
        // PROJBOBCAT: GameVersionHelper classifies the expanded raw version
        // metadata so loader detection follows the core's resolver logic.
        var loaderKind = rawVersion is null
            ? LoaderKind.Vanilla
            : GameVersionHelper.TryGetGameModLoaderType(rawVersion) switch
            {
                ModLoaderType.NeoForge => LoaderKind.NeoForge,
                ModLoaderType.Forge => LoaderKind.Forge,
                ModLoaderType.Fabric => LoaderKind.Fabric,
                ModLoaderType.Quilt => LoaderKind.Quilt,
                ModLoaderType.Optifine => LoaderKind.OptiFine,
                _ => DetectFallbackLoader(version.Id)
            };

        var loaderVersion = loaderKind switch
        {
            LoaderKind.Forge when rawVersion is not null
                => GameVersionHelper.TryGetForgeVersion(rawVersion),
            LoaderKind.NeoForge when rawVersion is not null
                => GameVersionHelper.TryGetNeoForgeVersion(rawVersion),
            LoaderKind.Fabric when rawVersion is not null
                => GameVersionHelper.TryGetFabricVersion(rawVersion),
            _ => null
        };

        return new GameVersionDescriptor(
            version.Id,
            string.IsNullOrWhiteSpace(version.Name) ? version.Id : version.Name,
            version.GameBaseVersion,
            loaderKind,
            loaderVersion,
            version.JavaVersion?.MajorVersion,
            version.JavaVersion?.Component,
            false);
    }

    private static LoaderKind DetectFallbackLoader(string versionId)
    {
        if (versionId.Contains("neoforge", StringComparison.OrdinalIgnoreCase))
        {
            return LoaderKind.NeoForge;
        }

        if (versionId.Contains("forge", StringComparison.OrdinalIgnoreCase))
        {
            return LoaderKind.Forge;
        }

        if (versionId.Contains("fabric", StringComparison.OrdinalIgnoreCase))
        {
            return LoaderKind.Fabric;
        }

        if (versionId.Contains("quilt", StringComparison.OrdinalIgnoreCase))
        {
            return LoaderKind.Quilt;
        }

        if (versionId.Contains("optifine", StringComparison.OrdinalIgnoreCase))
        {
            return LoaderKind.OptiFine;
        }

        if (versionId.Contains("liteloader", StringComparison.OrdinalIgnoreCase))
        {
            return LoaderKind.LiteLoader;
        }

        return LoaderKind.Vanilla;
    }
}
