using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Application;

public sealed class InstanceService(
    IGameRuntimeProvider runtimeProvider,
    IVersionPreferencesStore preferencesStore,
    IInstanceStorage instanceStorage) : IInstanceService
{
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public async Task<IReadOnlyList<GameInstance>> GetInstancesAsync(
        CancellationToken cancellationToken = default)
    {
        var runtime = await runtimeProvider.GetAsync(cancellationToken).ConfigureAwait(false);
        var versions = await runtime.GetVersionsAsync(cancellationToken).ConfigureAwait(false);
        var manifests = await instanceStorage.LoadManifestsAsync(cancellationToken).ConfigureAwait(false);
        var preferences = await preferencesStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        return versions
            .Select(version =>
            {
                var manifest = manifests.GetValueOrDefault(version.Id)
                               ?? InstanceManifestFactory.FromVersion(version);
                var preference = preferences.GetValueOrDefault(version.Id) ?? new VersionPreference();
                return new GameInstance(
                    version.Id,
                    manifest.Name,
                    manifest.MinecraftVersion,
                    manifest.LoaderKind,
                    manifest.LoaderVersion,
                    preference.IsFavorite,
                    preference.IsIsolated,
                    manifest.IsPlaceholder,
                    manifest.LaunchSettings);
            })
            .OrderBy(instance => instance.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(instance => instance.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public Task<InstanceManifest?> GetManifestAsync(
        string instanceId,
        CancellationToken cancellationToken = default)
    {
        return instanceStorage.LoadManifestAsync(instanceId, cancellationToken);
    }

    public async Task<InstanceOperationOutcome> CreateEmptyAsync(
        NewInstanceRequest request,
        CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var id = request.Id.Trim();
            string name;
            try
            {
                name = NormalizeName(request.Name, id);
            }
            catch (Exception exception) when (exception is InvalidDataException
                                                   or ArgumentException
                                                   or NotSupportedException
                                                   or PathTooLongException)
            {
                return new InstanceOperationOutcome(
                    false,
                    "Instances.InvalidName",
                    id,
                    exception.Message);
            }
            if (!ValidateLaunchSettings(new InstanceLaunchSettings(), out var error))
            {
                return new InstanceOperationOutcome(false, "Instances.InvalidSettings", error);
            }

            if (await instanceStorage.VersionExistsAsync(id, cancellationToken).ConfigureAwait(false))
            {
                return new InstanceOperationOutcome(false, "Instances.AlreadyExists", id);
            }

            var minecraftVersion = request.MinecraftVersion.Trim();
            if (minecraftVersion.Length == 0 || request.LoaderKind == LoaderKind.Other)
            {
                return new InstanceOperationOutcome(
                    false,
                    "Instances.InvalidVersion",
                    id,
                    "A Minecraft version and specific loader kind are required.");
            }

            var now = DateTimeOffset.UtcNow;
            var manifest = new InstanceManifest(
                id,
                name,
                minecraftVersion,
                request.LoaderKind,
                string.IsNullOrWhiteSpace(request.LoaderVersion) ? null : request.LoaderVersion.Trim(),
                new InstanceLaunchSettings(),
                true,
                now,
                now);
            try
            {
                await instanceStorage.CreatePlaceholderAsync(manifest, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                return new InstanceOperationOutcome(false, "Instances.CreateFailed", id, exception.Message);
            }

            await SetIsolationPreferenceAsync(id, true, cancellationToken).ConfigureAwait(false);
            return new InstanceOperationOutcome(true, "Instances.Created", id);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<InstanceOperationOutcome> SaveAsync(
        InstanceManifest manifest,
        CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!await instanceStorage.VersionExistsAsync(manifest.Id, cancellationToken).ConfigureAwait(false))
            {
                return new InstanceOperationOutcome(false, "Instances.NotFound", manifest.Id);
            }

            if (!ValidateLaunchSettings(manifest.LaunchSettings, out var error))
            {
                return new InstanceOperationOutcome(false, "Instances.InvalidSettings", manifest.Id, error);
            }

            var runtime = await runtimeProvider.GetAsync(cancellationToken).ConfigureAwait(false);
            var versions = await runtime.GetVersionsAsync(cancellationToken).ConfigureAwait(false);
            var installed = versions.FirstOrDefault(version =>
                string.Equals(version.Id, manifest.Id, StringComparison.OrdinalIgnoreCase));
            if (installed is not null &&
                !manifest.IsPlaceholder &&
                (!string.Equals(
                    installed.BaseVersion,
                    manifest.MinecraftVersion,
                    StringComparison.OrdinalIgnoreCase) ||
                 installed.LoaderKind != manifest.LoaderKind ||
                 !string.Equals(
                     installed.LoaderVersion,
                     manifest.LoaderVersion,
                     StringComparison.OrdinalIgnoreCase)))
            {
                return new InstanceOperationOutcome(
                    false,
                    "Instances.VersionMismatch",
                    manifest.Id,
                    $"Locator reports {installed.BaseVersion}/{installed.LoaderKind}.");
            }

            InstanceManifest normalized;
            try
            {
                normalized = manifest with
                {
                    Name = NormalizeName(manifest.Name, manifest.Id),
                    UpdatedAt = DateTimeOffset.UtcNow,
                    LaunchSettings = NormalizeLaunchSettings(manifest.LaunchSettings)
                };
            }
            catch (Exception exception) when (exception is InvalidDataException
                                                   or ArgumentException
                                                   or NotSupportedException
                                                   or PathTooLongException)
            {
                return new InstanceOperationOutcome(
                    false,
                    "Instances.InvalidName",
                    manifest.Id,
                    exception.Message);
            }

            await instanceStorage.SaveManifestAsync(normalized, cancellationToken).ConfigureAwait(false);
            return new InstanceOperationOutcome(true, "Instances.Saved", manifest.Id);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<InstanceOperationOutcome> DeleteAsync(
        string instanceId,
        CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!await instanceStorage.VersionExistsAsync(instanceId, cancellationToken).ConfigureAwait(false))
            {
                return new InstanceOperationOutcome(false, "Instances.NotFound", instanceId);
            }

            await instanceStorage.DeleteInstanceAsync(instanceId, cancellationToken).ConfigureAwait(false);
            await RemoveIsolationPreferenceAsync(instanceId, cancellationToken).ConfigureAwait(false);
            return new InstanceOperationOutcome(true, "Instances.Deleted", instanceId);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<InstanceOperationOutcome> CopyAsync(
        string sourceInstanceId,
        string targetInstanceId,
        string targetName,
        CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var runtime = await runtimeProvider.GetAsync(cancellationToken).ConfigureAwait(false);
            var versions = await runtime.GetVersionsAsync(cancellationToken).ConfigureAwait(false);
            var sourceVersion = versions.FirstOrDefault(version =>
                string.Equals(version.Id, sourceInstanceId, StringComparison.OrdinalIgnoreCase));
            if (sourceVersion is null)
            {
                return new InstanceOperationOutcome(false, "Instances.NotFound", sourceInstanceId);
            }

            if (await instanceStorage.VersionExistsAsync(targetInstanceId, cancellationToken).ConfigureAwait(false))
            {
                return new InstanceOperationOutcome(false, "Instances.AlreadyExists", targetInstanceId);
            }

            var sourceManifest = await instanceStorage
                                     .LoadManifestAsync(sourceInstanceId, cancellationToken)
                                     .ConfigureAwait(false)
                                 ?? InstanceManifestFactory.FromVersion(sourceVersion);
            try
            {
                await instanceStorage.CopyVersionAsync(
                    sourceInstanceId,
                    targetInstanceId,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                try
                {
                    if (await instanceStorage.VersionExistsAsync(
                            targetInstanceId,
                            CancellationToken.None).ConfigureAwait(false))
                    {
                        await instanceStorage.DeleteInstanceAsync(
                            targetInstanceId,
                            CancellationToken.None).ConfigureAwait(false);
                    }
                }
                catch
                {
                    // The original copy failure remains the primary error.
                }

                return new InstanceOperationOutcome(
                    false,
                    "Instances.CopyFailed",
                    targetInstanceId,
                    exception.Message);
            }

            var now = DateTimeOffset.UtcNow;
            InstanceManifest copied;
            try
            {
                copied = sourceManifest with
                {
                    Id = targetInstanceId,
                    Name = NormalizeName(targetName, targetInstanceId),
                    CreatedAt = now,
                    UpdatedAt = now
                };
            }
            catch (Exception exception) when (exception is InvalidDataException
                                                   or ArgumentException
                                                   or NotSupportedException
                                                   or PathTooLongException)
            {
                await instanceStorage.DeleteInstanceAsync(targetInstanceId, CancellationToken.None)
                    .ConfigureAwait(false);
                return new InstanceOperationOutcome(
                    false,
                    "Instances.InvalidName",
                    targetInstanceId,
                    exception.Message);
            }

            try
            {
                await instanceStorage.SaveManifestAsync(copied, cancellationToken).ConfigureAwait(false);
                var preferences = new Dictionary<string, VersionPreference>(
                    await preferencesStore.LoadAsync(cancellationToken).ConfigureAwait(false),
                    StringComparer.OrdinalIgnoreCase);
                preferences[targetInstanceId] =
                    preferences.GetValueOrDefault(sourceInstanceId) ?? new VersionPreference();
                await preferencesStore.SaveAsync(preferences, cancellationToken).ConfigureAwait(false);
                return new InstanceOperationOutcome(true, "Instances.Copied", targetInstanceId);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                await instanceStorage.DeleteInstanceAsync(targetInstanceId, CancellationToken.None)
                    .ConfigureAwait(false);
                return new InstanceOperationOutcome(
                    false,
                    "Instances.CopyFailed",
                    targetInstanceId,
                    exception.Message);
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    internal static bool ValidateLaunchSettings(
        InstanceLaunchSettings settings,
        out string? error)
    {
        if (settings.MinMemoryMb < 512 ||
            settings.MaxMemoryMb < settings.MinMemoryMb ||
            settings.MaxMemoryMb > 262144)
        {
            error = "Memory limits must satisfy 512 <= min <= max <= 262144 MiB.";
            return false;
        }

        if (settings.WindowWidth is < 320 or > 16384 ||
            settings.WindowHeight is < 240 or > 16384)
        {
            error = "Window dimensions are outside the supported range.";
            return false;
        }

        if (settings.JvmArguments?.Any(argument => argument.Contains('\0')) == true ||
            settings.GameArguments?.Any(argument => argument.Contains('\0')) == true)
        {
            error = "Launch arguments cannot contain NUL characters.";
            return false;
        }

        error = null;
        return true;
    }

    private static InstanceLaunchSettings NormalizeLaunchSettings(InstanceLaunchSettings settings)
    {
        return settings with
        {
            JavaExecutablePath = string.IsNullOrWhiteSpace(settings.JavaExecutablePath)
                ? null
                : Path.GetFullPath(settings.JavaExecutablePath),
            JvmArguments = settings.JvmArguments?
                .Where(argument => !string.IsNullOrWhiteSpace(argument))
                .Select(argument => argument.Trim())
                .ToArray() ?? [],
            GameArguments = settings.GameArguments?
                .Where(argument => !string.IsNullOrWhiteSpace(argument))
                .Select(argument => argument.Trim())
                .ToArray() ?? []
        };
    }

    private static string NormalizeName(string name, string fallback)
    {
        var normalized = string.IsNullOrWhiteSpace(name) ? fallback : name.Trim();
        if (normalized.Length > 64 || normalized.Any(char.IsControl))
        {
            throw new InvalidDataException("Instance names must be 1-64 characters without control characters.");
        }

        return normalized;
    }

    private async Task SetIsolationPreferenceAsync(
        string instanceId,
        bool isIsolated,
        CancellationToken cancellationToken)
    {
        var preferences = new Dictionary<string, VersionPreference>(
            await preferencesStore.LoadAsync(cancellationToken).ConfigureAwait(false),
            StringComparer.OrdinalIgnoreCase);
        preferences[instanceId] = preferences.GetValueOrDefault(instanceId) is { } existing
            ? existing with { IsIsolated = isIsolated }
            : new VersionPreference(IsIsolated: isIsolated);
        await preferencesStore.SaveAsync(preferences, cancellationToken).ConfigureAwait(false);
    }

    private async Task RemoveIsolationPreferenceAsync(
        string instanceId,
        CancellationToken cancellationToken)
    {
        var preferences = new Dictionary<string, VersionPreference>(
            await preferencesStore.LoadAsync(cancellationToken).ConfigureAwait(false),
            StringComparer.OrdinalIgnoreCase);
        if (preferences.Remove(instanceId))
        {
            await preferencesStore.SaveAsync(preferences, cancellationToken).ConfigureAwait(false);
        }
    }
}

internal static class InstanceManifestFactory
{
    public static InstanceManifest FromVersion(GameVersionDescriptor version)
    {
        var now = DateTimeOffset.UtcNow;
        return new InstanceManifest(
            version.Id,
            version.DisplayName,
            version.BaseVersion,
            version.LoaderKind,
            version.LoaderVersion,
            new InstanceLaunchSettings(JavaExecutablePath: null, SuggestedJavaMajor: version.JavaMajorVersion),
            false,
            now,
            now);
    }
}
