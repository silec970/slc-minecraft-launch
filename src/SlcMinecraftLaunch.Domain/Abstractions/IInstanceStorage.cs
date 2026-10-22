using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Domain.Abstractions;

public interface IInstanceStorage
{
    Task<InstanceManifest?> LoadManifestAsync(
        string instanceId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, InstanceManifest>> LoadManifestsAsync(
        CancellationToken cancellationToken = default);

    Task SaveManifestAsync(
        InstanceManifest manifest,
        CancellationToken cancellationToken = default);

    Task<bool> VersionExistsAsync(
        string instanceId,
        CancellationToken cancellationToken = default);

    Task CreatePlaceholderAsync(
        InstanceManifest manifest,
        CancellationToken cancellationToken = default);

    Task DeleteInstanceAsync(
        string instanceId,
        CancellationToken cancellationToken = default);

    Task CopyVersionAsync(
        string sourceInstanceId,
        string targetInstanceId,
        CancellationToken cancellationToken = default);

    Task ExportAsync(
        InstanceManifest manifest,
        bool versionIsolation,
        InstanceExportOptions options,
        string destinationPath,
        CancellationToken cancellationToken = default);

    Task<ImportedInstanceArchive> InspectArchiveAsync(
        string archivePath,
        CancellationToken cancellationToken = default);

    Task ExtractArchiveDataAsync(
        ImportedInstanceArchive archive,
        bool versionIsolation,
        CancellationToken cancellationToken = default);
}
