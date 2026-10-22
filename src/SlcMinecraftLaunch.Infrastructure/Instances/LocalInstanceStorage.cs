using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using ProjBobcat.Class.Helper;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;
using SlcMinecraftLaunch.Infrastructure.Configuration;

namespace SlcMinecraftLaunch.Infrastructure.Instances;

public sealed class LocalInstanceStorage(PortableLauncherPaths paths) : IInstanceStorage
{
    private static readonly HashSet<string> CopiedVersionDataRoots = new(
        [
            "mods",
            "config",
            "resourcepacks",
            "shaderpacks",
            "datapacks",
            "scripts",
            "structures",
            "saves",
            "screenshots",
            "logs",
            "crash-reports"
        ],
        StringComparer.OrdinalIgnoreCase);

    private static readonly string[] DefaultGameplayDirectories =
    [
        "resourcepacks",
        "shaderpacks",
        "datapacks",
        "scripts",
        "structures"
    ];

    public async Task<InstanceManifest?> LoadManifestAsync(
        string instanceId,
        CancellationToken cancellationToken = default)
    {
        var versionDirectory = GetVersionDirectory(instanceId);
        var manifestPath = Path.Combine(versionDirectory, "instance.json");
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        try
        {
            await using var input = File.OpenRead(manifestPath);
            return await JsonSerializer.DeserializeAsync<InstanceManifest>(
                input,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public async Task<IReadOnlyDictionary<string, InstanceManifest>> LoadManifestsAsync(
        CancellationToken cancellationToken = default)
    {
        var versionsRoot = Path.Combine(paths.GameRoot, "versions");
        if (!Directory.Exists(versionsRoot))
        {
            return new Dictionary<string, InstanceManifest>(StringComparer.OrdinalIgnoreCase);
        }

        var manifests = new Dictionary<string, InstanceManifest>(StringComparer.OrdinalIgnoreCase);
        foreach (var instanceId in Directory.EnumerateDirectories(versionsRoot)
                     .Select(Path.GetFileName)
                     .OfType<string>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var manifest = await LoadManifestAsync(instanceId, cancellationToken).ConfigureAwait(false);
            if (manifest is not null)
            {
                manifests[manifest.Id] = manifest;
            }
        }

        return manifests;
    }

    public async Task SaveManifestAsync(
        InstanceManifest manifest,
        CancellationToken cancellationToken = default)
    {
        var versionDirectory = GetVersionDirectory(manifest.Id);
        if (!Directory.Exists(versionDirectory))
        {
            throw new DirectoryNotFoundException($"Instance version '{manifest.Id}' does not exist.");
        }

        await WriteJsonAsync(
            Path.Combine(versionDirectory, "instance.json"),
            manifest,
            cancellationToken).ConfigureAwait(false);
    }

    public Task<bool> VersionExistsAsync(
        string instanceId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Directory.Exists(GetVersionDirectory(instanceId)));
    }

    public async Task CreatePlaceholderAsync(
        InstanceManifest manifest,
        CancellationToken cancellationToken = default)
    {
        var versionDirectory = GetVersionDirectory(manifest.Id);
        if (Directory.Exists(versionDirectory))
        {
            throw new IOException($"Instance version '{manifest.Id}' already exists.");
        }

        Directory.CreateDirectory(versionDirectory);
        // PROJBOBCAT: The version directory follows GamePathHelper's locator
        // contract and contains only the approved minimal placeholder JSON.
        var placeholder = new JsonObject
        {
            ["id"] = manifest.Id,
            ["type"] = "release",
            ["libraries"] = new JsonArray(),
            ["downloads"] = new JsonObject()
        };
        await WriteJsonNodeAsync(
            Path.Combine(versionDirectory, $"{manifest.Id}.json"),
            placeholder,
            cancellationToken).ConfigureAwait(false);
        await SaveManifestAsync(manifest, cancellationToken).ConfigureAwait(false);
    }

    public Task DeleteInstanceAsync(
        string instanceId,
        CancellationToken cancellationToken = default)
    {
        var versionDirectory = GetVersionDirectory(instanceId);
        if (!Directory.Exists(versionDirectory))
        {
            throw new DirectoryNotFoundException($"Instance version '{instanceId}' does not exist.");
        }

        return Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.Delete(versionDirectory, true);
        }, cancellationToken);
    }

    public async Task CopyVersionAsync(
        string sourceInstanceId,
        string targetInstanceId,
        CancellationToken cancellationToken = default)
    {
        var sourceDirectory = GetVersionDirectory(sourceInstanceId);
        var targetDirectory = GetVersionDirectory(targetInstanceId);
        if (!Directory.Exists(sourceDirectory))
        {
            throw new DirectoryNotFoundException($"Source instance '{sourceInstanceId}' does not exist.");
        }

        if (Directory.Exists(targetDirectory))
        {
            throw new IOException($"Target instance '{targetInstanceId}' already exists.");
        }

        await CopyDirectoryAsync(
            sourceDirectory,
            targetDirectory,
            static (_, name) =>
                !CopiedVersionDataRoots.Contains(name) &&
                !string.Equals(name, "instance.json", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(name, "loader.json", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(name, "options.txt", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(name, "servers.dat", StringComparison.OrdinalIgnoreCase),
            cancellationToken).ConfigureAwait(false);

        var sourceJsonPath = Path.Combine(sourceDirectory, $"{sourceInstanceId}.json");
        if (!File.Exists(sourceJsonPath))
        {
            Directory.Delete(targetDirectory, true);
            throw new InvalidDataException("The source instance has no version JSON.");
        }

        var node = JsonNode.Parse(await File.ReadAllTextAsync(
            sourceJsonPath,
            cancellationToken).ConfigureAwait(false)) as JsonObject
                   ?? throw new InvalidDataException("The source version JSON is invalid.");
        node["id"] = targetInstanceId;
        await WriteJsonNodeAsync(
            Path.Combine(targetDirectory, $"{targetInstanceId}.json"),
            node,
            cancellationToken).ConfigureAwait(false);
        var copiedSourceJson = Path.Combine(targetDirectory, $"{sourceInstanceId}.json");
        if (!string.Equals(sourceInstanceId, targetInstanceId, StringComparison.OrdinalIgnoreCase) &&
            File.Exists(copiedSourceJson))
        {
            File.Delete(copiedSourceJson);
        }
    }

    public Task ExportAsync(
        InstanceManifest manifest,
        bool versionIsolation,
        InstanceExportOptions options,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () => ExportCore(manifest, versionIsolation, options, destinationPath, cancellationToken),
            cancellationToken);
    }

    public Task<ImportedInstanceArchive> InspectArchiveAsync(
        string archivePath,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () => InspectArchiveCore(archivePath, cancellationToken),
            cancellationToken);
    }

    public Task ExtractArchiveDataAsync(
        ImportedInstanceArchive archive,
        bool versionIsolation,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () => ExtractArchiveDataCore(archive, versionIsolation, cancellationToken),
            cancellationToken);
    }

    private void ExportCore(
        InstanceManifest manifest,
        bool versionIsolation,
        InstanceExportOptions options,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var versionDirectory = GetVersionDirectory(manifest.Id);
        if (!Directory.Exists(versionDirectory))
        {
            throw new DirectoryNotFoundException($"Instance '{manifest.Id}' does not exist.");
        }

        var destinationFullPath = Path.GetFullPath(destinationPath);
        var normalizedGameRoot = Path.GetFullPath(paths.GameRoot) + Path.DirectorySeparatorChar;
        if (destinationFullPath.StartsWith(normalizedGameRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Export destination cannot be inside the game root.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destinationFullPath)!);
        var temporaryPath = $"{destinationFullPath}.{Guid.NewGuid():N}.tmp";
        var dataRoot = versionIsolation ? versionDirectory : paths.GameRoot;
        var addedEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using (var archive = ZipFile.Open(temporaryPath, ZipArchiveMode.Create))
            {
                WriteJsonEntry(archive, "instance.json", manifest);
                WriteJsonEntry(
                    archive,
                    "loader.json",
                    new InstanceLoaderDeclaration(
                        manifest.Id,
                        manifest.MinecraftVersion,
                        manifest.LoaderKind,
                        manifest.LoaderVersion));

                var versionJsonPath = Path.Combine(versionDirectory, $"{manifest.Id}.json");
                AddFile(archive, versionJsonPath, "version.json", addedEntries);
                AddDirectoryFiles(
                    archive,
                    Path.Combine(dataRoot, "mods"),
                    "mods",
                    "*.jar",
                    addedEntries,
                    cancellationToken);
                AddDirectoryFiles(
                    archive,
                    Path.Combine(dataRoot, "config"),
                    "config",
                    "*",
                    addedEntries,
                    cancellationToken);

                var modSourcesPath = Path.Combine(paths.DataRoot, "mod-sources.json");
                AddFile(archive, modSourcesPath, "mod-sources.json", addedEntries);

                if (options.IncludeDefaultGameplayData)
                {
                    foreach (var directory in DefaultGameplayDirectories)
                    {
                        AddDirectoryFiles(
                            archive,
                            Path.Combine(dataRoot, directory),
                            directory,
                            "*",
                            addedEntries,
                            cancellationToken);
                    }

                    AddFile(
                        archive,
                        Path.Combine(dataRoot, "options.txt"),
                        "options.txt",
                        addedEntries);
                }

                AddOptionalData(
                    archive,
                    dataRoot,
                    options.IncludeSaves,
                    "saves",
                    FileOrDirectory.Directory,
                    addedEntries,
                    cancellationToken);
                AddOptionalData(
                    archive,
                    dataRoot,
                    options.IncludeScreenshots,
                    "screenshots",
                    FileOrDirectory.Directory,
                    addedEntries,
                    cancellationToken);
                AddOptionalData(
                    archive,
                    dataRoot,
                    options.IncludeLogs,
                    "logs",
                    FileOrDirectory.Directory,
                    addedEntries,
                    cancellationToken);
                AddOptionalData(
                    archive,
                    dataRoot,
                    options.IncludeLogs,
                    "crash-reports",
                    FileOrDirectory.Directory,
                    addedEntries,
                    cancellationToken);
                AddOptionalData(
                    archive,
                    dataRoot,
                    options.IncludeServersFile,
                    "servers.dat",
                    FileOrDirectory.File,
                    addedEntries,
                    cancellationToken);
            }

            File.Move(temporaryPath, destinationFullPath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private ImportedInstanceArchive InspectArchiveCore(
        string archivePath,
        CancellationToken cancellationToken)
    {
        using var archive = ZipFile.OpenRead(Path.GetFullPath(archivePath));
        ValidateArchiveEntries(archive);
        var manifest = ReadJsonEntry<InstanceManifest>(archive, "instance.json")
                       ?? throw new InvalidDataException("The archive has no instance.json.");
        EnsureSafeInstanceId(manifest.Id);
        if (string.IsNullOrWhiteSpace(manifest.Name) ||
            manifest.Name.Length > 64 ||
            manifest.Name.Any(char.IsControl) ||
            string.IsNullOrWhiteSpace(manifest.MinecraftVersion) ||
            manifest.LoaderKind == LoaderKind.Other)
        {
            throw new InvalidDataException("The imported instance metadata is invalid.");
        }

        var loader = ReadJsonEntry<InstanceLoaderDeclaration>(archive, "loader.json")
                     ?? throw new InvalidDataException("The archive has no loader.json.");
        if (!string.Equals(manifest.Id, loader.InstanceId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                manifest.MinecraftVersion,
                loader.MinecraftVersion,
                StringComparison.OrdinalIgnoreCase) ||
            manifest.LoaderKind != loader.LoaderKind ||
            !string.Equals(
                manifest.LoaderVersion,
                loader.LoaderVersion,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The loader declaration doesn't match instance.json.");
        }

        var modSources = ReadJsonEntry<Dictionary<string, ModSourceMetadata>>(
                             archive,
                             "mod-sources.json")
                         ?? new Dictionary<string, ModSourceMetadata>(StringComparer.OrdinalIgnoreCase);
        cancellationToken.ThrowIfCancellationRequested();
        return new ImportedInstanceArchive(
            Path.GetFullPath(archivePath),
            manifest,
            loader,
            new Dictionary<string, ModSourceMetadata>(modSources, StringComparer.OrdinalIgnoreCase));
    }

    private void ExtractArchiveDataCore(
        ImportedInstanceArchive imported,
        bool versionIsolation,
        CancellationToken cancellationToken)
    {
        var versionDirectory = GetVersionDirectory(imported.Manifest.Id);
        if (!Directory.Exists(versionDirectory))
        {
            throw new DirectoryNotFoundException($"Imported instance '{imported.Manifest.Id}' is not prepared.");
        }

        var dataRoot = versionIsolation ? versionDirectory : paths.GameRoot;
        using var archive = ZipFile.OpenRead(imported.ArchivePath);
        ValidateArchiveEntries(archive);
        var fileEntries = archive.Entries
            .Where(entry => !string.IsNullOrEmpty(entry.Name))
            .Where(entry => !string.Equals(entry.FullName, "instance.json", StringComparison.OrdinalIgnoreCase))
            .Where(entry => !string.Equals(entry.FullName, "loader.json", StringComparison.OrdinalIgnoreCase))
            .Where(entry => !string.Equals(entry.FullName, "version.json", StringComparison.OrdinalIgnoreCase))
            .Where(entry => !string.Equals(entry.FullName, "mod-sources.json", StringComparison.OrdinalIgnoreCase))
            .Select(entry => (
                Entry: entry,
                Destination: GetSafeChildPath(
                    dataRoot,
                    entry.FullName.Replace('/', Path.DirectorySeparatorChar))))
            .ToArray();
        var conflict = fileEntries.FirstOrDefault(item => File.Exists(item.Destination));
        if (conflict.Entry is not null)
        {
            throw new IOException($"Import conflict: '{conflict.Entry.FullName}' already exists.");
        }

        foreach (var (entry, destination) in fileEntries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            entry.ExtractToFile(destination, overwrite: false);
        }

        File.WriteAllText(
            Path.Combine(versionDirectory, "instance.json"),
            JsonSerializer.Serialize(imported.Manifest, new JsonSerializerOptions { WriteIndented = true }));
        File.WriteAllText(
            Path.Combine(versionDirectory, "loader.json"),
            JsonSerializer.Serialize(imported.LoaderDeclaration, new JsonSerializerOptions { WriteIndented = true }));
    }

    private string GetVersionDirectory(string instanceId)
    {
        EnsureSafeInstanceId(instanceId);
        // PROJBOBCAT: GamePathHelper.GetGamePath is the locator-owned version
        // isolation path contract; instance operations never hard-code a root.
        var versionDirectory = Path.GetFullPath(Path.Combine(
            paths.GameRoot,
            GamePathHelper.GetGamePath(instanceId)));
        var versionsRoot = Path.GetFullPath(Path.Combine(paths.GameRoot, "versions")) +
                           Path.DirectorySeparatorChar;
        if (!versionDirectory.StartsWith(versionsRoot, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                Path.TrimEndingDirectorySeparator(versionDirectory),
                Path.TrimEndingDirectorySeparator(versionsRoot),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Instance path escaped the versions directory.");
        }

        return versionDirectory;
    }

    private static void EnsureSafeInstanceId(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId) ||
            instanceId.Length > 64 ||
            instanceId is "." or ".." ||
            instanceId.EndsWith('.') ||
            instanceId.EndsWith(' ') ||
            instanceId.Any(character =>
                char.IsControl(character) ||
                Path.GetInvalidFileNameChars().Contains(character)) ||
            instanceId.Contains(Path.DirectorySeparatorChar) ||
            instanceId.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new InvalidDataException("The instance ID is not a safe directory name.");
        }

        var reserved = new HashSet<string>(
            [
                "CON", "PRN", "AUX", "NUL",
                "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
                "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
            ],
            StringComparer.OrdinalIgnoreCase);
        var deviceBaseName = instanceId.Split('.', 2)[0];
        if (reserved.Contains(deviceBaseName))
        {
            throw new InvalidDataException("The instance ID is reserved by Windows.");
        }
    }

    private static async Task CopyDirectoryAsync(
        string sourceDirectory,
        string targetDirectory,
        Func<string, string, bool> include,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(targetDirectory);
        foreach (var sourcePath in Directory.EnumerateFileSystemEntries(sourceDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = Path.GetFileName(sourcePath);
            if (!include(sourcePath, name))
            {
                continue;
            }

            var targetPath = Path.Combine(targetDirectory, name);
            if (Directory.Exists(sourcePath))
            {
                await CopyDirectoryAsync(
                    sourcePath,
                    targetPath,
                    static (_, _) => true,
                    cancellationToken).ConfigureAwait(false);
            }
            else if (File.Exists(sourcePath))
            {
                await using var source = File.OpenRead(sourcePath);
                await using var target = File.Create(targetPath);
                await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static void AddDirectoryFiles(
        ZipArchive archive,
        string directory,
        string entryPrefix,
        string searchPattern,
        ISet<string> addedEntries,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var filePath in Directory.EnumerateFiles(directory, searchPattern, SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relativePath = Path.GetRelativePath(directory, filePath).Replace('\\', '/');
            AddFile(archive, filePath, $"{entryPrefix}/{relativePath}", addedEntries);
        }
    }

    private static void AddFile(
        ZipArchive archive,
        string filePath,
        string entryName,
        ISet<string> addedEntries)
    {
        if (!File.Exists(filePath) || !addedEntries.Add(entryName))
        {
            return;
        }

        archive.CreateEntryFromFile(filePath, entryName, CompressionLevel.Optimal);
    }

    private static void AddOptionalData(
        ZipArchive archive,
        string dataRoot,
        bool include,
        string name,
        FileOrDirectory kind,
        ISet<string> addedEntries,
        CancellationToken cancellationToken)
    {
        if (!include)
        {
            return;
        }

        if (kind == FileOrDirectory.File)
        {
            AddFile(archive, Path.Combine(dataRoot, name), name, addedEntries);
            return;
        }

        AddDirectoryFiles(
            archive,
            Path.Combine(dataRoot, name),
            name,
            "*",
            addedEntries,
            cancellationToken);
    }

    private static void WriteJsonEntry<T>(ZipArchive archive, string entryName, T value)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var output = new StreamWriter(entry.Open());
        output.Write(JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static T? ReadJsonEntry<T>(ZipArchive archive, string entryName)
    {
        var entry = archive.GetEntry(entryName);
        if (entry is null)
        {
            return default;
        }

        using var input = entry.Open();
        return JsonSerializer.Deserialize<T>(input);
    }

    private static void ValidateArchiveEntries(ZipArchive archive)
    {
        foreach (var entry in archive.Entries)
        {
            var normalized = entry.FullName.Replace('\\', '/');
            if (normalized.StartsWith('/') ||
                normalized.Split('/').Any(segment => segment is ".." or "."))
            {
                throw new InvalidDataException($"Unsafe archive entry '{entry.FullName}'.");
            }
        }
    }

    private static string GetSafeChildPath(string rootPath, string relativePath)
    {
        var normalizedRoot = Path.GetFullPath(rootPath) + Path.DirectorySeparatorChar;
        var fullPath = Path.GetFullPath(Path.Combine(rootPath, relativePath));
        if (!fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Archive entry escaped its target directory.");
        }

        return fullPath;
    }

    private async Task WriteJsonAsync<T>(
        string path,
        T value,
        CancellationToken cancellationToken)
    {
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (var output = File.Create(temporaryPath))
            {
                await JsonSerializer.SerializeAsync(
                    output,
                    value,
                    new JsonSerializerOptions { WriteIndented = true },
                    cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporaryPath, path, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private async Task WriteJsonNodeAsync(
        string path,
        JsonNode value,
        CancellationToken cancellationToken)
    {
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(
                temporaryPath,
                value.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
                cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, path, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private enum FileOrDirectory
    {
        File,
        Directory
    }
}
