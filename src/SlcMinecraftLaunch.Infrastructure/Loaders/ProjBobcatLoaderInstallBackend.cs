using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using ProjBobcat.Class;
using ProjBobcat.Class.Helper;
using ProjBobcat.Class.Model;
using ProjBobcat.Class.Model.Fabric;
using ProjBobcat.Class.Model.Optifine;
using ProjBobcat.DefaultComponent.Installer;
using ProjBobcat.DefaultComponent.Installer.ForgeInstaller;
using ProjBobcat.Event;
using ProjBobcat.Interface;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;
using SlcMinecraftLaunch.Infrastructure.Configuration;
using SlcMinecraftLaunch.Infrastructure.ProjBobcat;

namespace SlcMinecraftLaunch.Infrastructure.Loaders;

public sealed class ProjBobcatLoaderInstallBackend(
    PortableLauncherPaths paths,
    IHttpClientFactory httpClientFactory,
    ProjBobcatRuntimeProvider runtimeProvider,
    LoaderSourceOptions sourceOptions) : ILoaderInstallBackend
{
    public async Task<LoaderInstallOutcome> InstallAsync(
        LoaderInstallPlan plan,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var runtime = await runtimeProvider.GetRuntimeAsync(cancellationToken).ConfigureAwait(false);
            var versionLocator = runtime.VersionLocator;
            return plan.LoaderKind switch
            {
                LoaderKind.Forge => await InstallForgeAsync(plan, versionLocator, progress, cancellationToken)
                    .ConfigureAwait(false),
                LoaderKind.Fabric => await InstallFabricAsync(plan, versionLocator, progress, cancellationToken)
                    .ConfigureAwait(false),
                LoaderKind.OptiFine => await InstallOptiFineAsync(plan, versionLocator, progress, cancellationToken)
                    .ConfigureAwait(false),
                LoaderKind.NeoForge => await InstallNeoForgeAsync(plan, versionLocator, progress, cancellationToken)
                    .ConfigureAwait(false),
                _ => new LoaderInstallOutcome(false, "Loader.NativeInstallerUnavailable")
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new LoaderInstallOutcome(
                false,
                "Loader.InstallFailed",
                ErrorDetail: exception.ToString());
        }
    }

    private async Task<LoaderInstallOutcome> InstallForgeAsync(
        LoaderInstallPlan plan,
        IVersionLocator versionLocator,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        var fullVersion = $"{plan.MinecraftVersion}-{plan.LoaderVersion}";
        var installerUrl = sourceOptions.ForgeDownloadUrlTemplate
            .Replace("{full}", fullVersion, StringComparison.Ordinal)
            .Replace("{loader}", plan.LoaderVersion, StringComparison.Ordinal);
        var installerPath = GetLoaderCachePath("forge", fullVersion, "installer.jar");
        await DownloadFileAsync(
            installerUrl,
            installerPath,
            progress,
            0.3,
            0.55,
            cancellationToken).ConfigureAwait(false);

        // HighVersionForgeInstaller writes its generated JSON without creating
        // the target version directory. Create only that locator-owned target.
        Directory.CreateDirectory(Path.Combine(
            paths.GameRoot,
            "versions",
            Sanitize(plan.CustomVersionId)));

        if (ForgeInstallerFactory.IsLegacyForgeInstaller(installerPath, plan.LoaderVersion))
        {
            // PROJBOBCAT: LegacyForgeInstaller owns legacy Forge profile parsing
            // and installation; the downloaded JAR is never executed directly.
            var legacyInstaller = new LegacyForgeInstaller
            {
                HttpClientFactory = httpClientFactory,
                RootPath = paths.GameRoot,
                ForgeVersion = plan.LoaderVersion,
                ForgeExecutablePath = installerPath,
                CustomId = plan.CustomVersionId,
                InheritsFrom = plan.MinecraftVersion
            };
            legacyInstaller.StageChangedEventDelegate += (_, eventArgs) =>
                ReportInstallerProgress(progress, eventArgs, 0.55, 0.95);

            var result = await legacyInstaller.InstallForgeTaskAsync().ConfigureAwait(false);
            return ToOutcome(result, plan.CustomVersionId);
        }

        // PROJBOBCAT: HighVersionForgeInstaller owns the modern install_profile
        // and processor chain through the public native installer model.
        var forgeInstaller = new HighVersionForgeInstaller
        {
            HttpClientFactory = httpClientFactory,
            RootPath = paths.GameRoot,
            ForgeExecutablePath = installerPath,
            JavaExecutablePath = plan.JavaPath,
            MineCraftVersionId = plan.MinecraftVersion,
            MineCraftVersion = plan.MinecraftVersion,
            VersionLocator = (VersionLocatorBase)versionLocator,
            CustomId = plan.CustomVersionId,
            InheritsFrom = plan.MinecraftVersion,
            DownloadThread = 16
        };
        forgeInstaller.StageChangedEventDelegate += (_, eventArgs) =>
            ReportInstallerProgress(progress, eventArgs, 0.55, 0.95);

        var highVersionResult = await forgeInstaller.InstallForgeTaskAsync().ConfigureAwait(false);
        return ToOutcome(highVersionResult, plan.CustomVersionId);
    }

    private async Task<LoaderInstallOutcome> InstallFabricAsync(
        LoaderInstallPlan plan,
        IVersionLocator versionLocator,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient();
        var artifactUrl = sourceOptions.FabricArtifactUrlTemplate
            .Replace("{minecraft}", plan.MinecraftVersion, StringComparison.Ordinal)
            .Replace("{loader}", plan.LoaderVersion, StringComparison.Ordinal);
        var artifact = await client.GetFromJsonAsync<FabricLoaderArtifactModel>(
            artifactUrl,
            cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("Fabric returned an empty loader artifact.");

        // PROJBOBCAT: FabricInstaller generates the inherited version JSON
        // through the core's native Fabric installer model.
        var installer = new FabricInstaller
        {
            HttpClientFactory = httpClientFactory,
            RootPath = paths.GameRoot,
            VersionLocator = versionLocator,
            LoaderArtifact = artifact,
            CustomId = plan.CustomVersionId,
            InheritsFrom = plan.MinecraftVersion
        };
        installer.StageChangedEventDelegate += (_, eventArgs) =>
            ReportInstallerProgress(progress, eventArgs, 0.3, 0.95);

        var installedVersionId = await installer.InstallTaskAsync().ConfigureAwait(false);
        return new LoaderInstallOutcome(true, "Loader.Installed", installedVersionId);
    }

    private async Task<LoaderInstallOutcome> InstallOptiFineAsync(
        LoaderInstallPlan plan,
        IVersionLocator versionLocator,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient();
        var listUrl = sourceOptions.OptiFineListUrlTemplate.Replace(
            "{minecraft}",
            plan.MinecraftVersion,
            StringComparison.Ordinal);
        var versions = await client.GetFromJsonAsync<OptifineDownloadVersionModel[]>(
            listUrl,
            cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidDataException("OptiFine returned an empty version list.");
        var selected = versions.FirstOrDefault(version =>
            string.Equals(
                $"{version.Type}_{version.Patch}",
                plan.LoaderVersion,
                StringComparison.OrdinalIgnoreCase));
        selected ??= versions.FirstOrDefault(version =>
            !version.Patch.StartsWith("pre", StringComparison.OrdinalIgnoreCase));
        if (selected is null)
        {
            return new LoaderInstallOutcome(false, "Loader.VersionNotFound", ErrorDetail: plan.LoaderVersion);
        }

        var downloadUrl = sourceOptions.OptiFineDownloadUrlTemplate
            .Replace("{minecraft}", selected.McVersion, StringComparison.Ordinal)
            .Replace("{type}", selected.Type, StringComparison.Ordinal)
            .Replace("{patch}", selected.Patch, StringComparison.Ordinal);
        var installerPath = GetLoaderCachePath("optifine", selected.FileName, selected.FileName);
        await DownloadFileAsync(
            downloadUrl,
            installerPath,
            progress,
            0.3,
            0.55,
            cancellationToken).ConfigureAwait(false);

        // PROJBOBCAT: OptifineInstaller owns OptiFine metadata and classpath
        // generation through the native installer model.
        var installer = new OptifineInstaller
        {
            HttpClientFactory = httpClientFactory,
            RootPath = paths.GameRoot,
            JavaExecutablePath = plan.JavaPath,
            OptifineJarPath = installerPath,
            OptifineDownloadVersion = selected,
            CustomId = plan.CustomVersionId,
            InheritsFrom = plan.MinecraftVersion
        };
        installer.StageChangedEventDelegate += (_, eventArgs) =>
            ReportInstallerProgress(progress, eventArgs, 0.55, 0.95);

        var installedVersionId = await installer.InstallTaskAsync().ConfigureAwait(false);
        return new LoaderInstallOutcome(true, "Loader.Installed", installedVersionId);
    }

    private async Task<LoaderInstallOutcome> InstallNeoForgeAsync(
        LoaderInstallPlan plan,
        IVersionLocator versionLocator,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        var downloadUrl = sourceOptions.NeoForgeDownloadUrlTemplate.Replace(
            "{loader}",
            plan.LoaderVersion,
            StringComparison.Ordinal);
        var installerPath = GetLoaderCachePath(
            "neoforge",
            plan.LoaderVersion,
            $"neoforge-{plan.LoaderVersion}-installer.jar");
        await DownloadFileAsync(
            downloadUrl,
            installerPath,
            progress,
            0.3,
            0.5,
            cancellationToken).ConfigureAwait(false);

        var output = new ConcurrentQueue<string>();
        var startInfo = new ProcessStartInfo(plan.JavaPath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = paths.GameRoot
        };
        startInfo.ArgumentList.Add("-jar");
        startInfo.ArgumentList.Add(installerPath);
        startInfo.ArgumentList.Add("--installClient");
        startInfo.ArgumentList.Add(paths.GameRoot);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Unable to start the NeoForge installer.");
        var lineCount = 0;
        process.OutputDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is null)
            {
                return;
            }

            output.Enqueue(eventArgs.Data);
            var count = Interlocked.Increment(ref lineCount);
            progress?.Report(new OperationProgress(
                SmokeTestStage.InstallingLoader,
                "Progress.NeoForgeInstaller",
                0.5 + (0.4 * OperationProgress.NormalizeFraction(count / 120.0)),
                Detail: plan.LoaderVersion));
        };
        process.ErrorDataReceived += (_, eventArgs) =>
        {
            if (eventArgs.Data is not null)
            {
                output.Enqueue(eventArgs.Data);
            }
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            throw;
        }

        var tail = string.Join(Environment.NewLine, output.TakeLast(40));
        if (process.ExitCode != 0)
        {
            return new LoaderInstallOutcome(
                false,
                "Loader.NeoForgeInstallerFailed",
                ErrorDetail: $"Exit code {process.ExitCode}{Environment.NewLine}{tail}");
        }

        var installed = versionLocator.GetAllGames()
            .OfType<VersionInfo>()
            .FirstOrDefault(version =>
                version.RawVersion is not null &&
                GameVersionHelper.TryGetNeoForgeVersion(version.RawVersion)
                    ?.Equals(plan.LoaderVersion, StringComparison.OrdinalIgnoreCase) == true);
        if (installed is null)
        {
            return new LoaderInstallOutcome(
                false,
                "Loader.VersionNotResolvable",
                ErrorDetail: tail);
        }

        return new LoaderInstallOutcome(true, "Loader.Installed", installed.Id);
    }

    private string GetLoaderCachePath(string loader, string version, string fileName)
    {
        var directory = Path.Combine(paths.GameRoot, "downloads", "loaders", loader, Sanitize(version));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, Sanitize(fileName));
    }

    private async Task DownloadFileAsync(
        string url,
        string destinationPath,
        IProgress<OperationProgress>? progress,
        double start,
        double end,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient();
        using var response = await client.GetAsync(
            url,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength;
        var temporaryPath = $"{destinationPath}.{Guid.NewGuid():N}.tmp";

        try
        {
            {
                await using var source = await response.Content
                    .ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await using var destination = new FileStream(
                    temporaryPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                var buffer = new byte[81920];
                long received = 0;

                while (true)
                {
                    var read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken)
                        .ConfigureAwait(false);
                    received += read;
                    var ratio = total is > 0
                        ? (double)received / total.Value
                        : 0;
                    progress?.Report(new OperationProgress(
                        SmokeTestStage.InstallingLoader,
                        "Progress.DownloadingLoader",
                        start + ((end - start) * OperationProgress.NormalizeFraction(ratio)),
                        received,
                        total));
                }
            }

            File.Move(temporaryPath, destinationPath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static void ReportInstallerProgress(
        IProgress<OperationProgress>? progress,
        StageChangedEventArgs eventArgs,
        double start,
        double end)
    {
        progress?.Report(new OperationProgress(
            SmokeTestStage.InstallingLoader,
            "Progress.InstallingLoader",
            start + ((end - start) * OperationProgress.NormalizeFraction(eventArgs.Progress.NormalizedValue)),
            Detail: eventArgs.CurrentStage));
    }

    private static LoaderInstallOutcome ToOutcome(ForgeInstallResult result, string installedVersionId)
    {
        return result.Succeeded
            ? new LoaderInstallOutcome(true, "Loader.Installed", installedVersionId)
            : new LoaderInstallOutcome(
                false,
                "Loader.ForgeInstallFailed",
                ErrorDetail: result.Error?.Exception?.ToString()
                             ?? result.Error?.Cause
                             ?? result.Error?.ErrorMessage
                             ?? "Forge installer failed.");
    }

    private static string Sanitize(string value)
    {
        return string.Concat(value.Select(character =>
            Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
    }
}
