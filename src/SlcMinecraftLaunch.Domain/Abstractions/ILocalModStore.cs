using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Domain.Abstractions;

public interface ILocalModStore
{
    string GetModsDirectory(
        string rootPath,
        string versionId,
        bool versionIsolation);

    Task<IReadOnlyList<ModEntry>> ScanAsync(
        string rootPath,
        string versionId,
        bool versionIsolation,
        CancellationToken cancellationToken = default);

    Task<ModEntry> ImportAsync(
        string rootPath,
        string versionId,
        bool versionIsolation,
        string sourceFilePath,
        CancellationToken cancellationToken = default);
}

