using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;
using SlcMinecraftLaunch.Infrastructure.Configuration;

namespace SlcMinecraftLaunch.Application;

public sealed record CommandLineExecutionResult(bool Handled, int ExitCode);

public sealed class CommandLineExecutionService(
    LauncherRuntimeConfiguration runtimeConfiguration,
    ICommandResultWriter resultWriter,
    IVersionService versionService,
    IJavaRuntimeProvisioner javaRuntimeProvisioner,
    ILoaderInstallationService loaderInstallationService,
    ICrashAnalysisService crashAnalysisService,
    IModService modService,
    ISmokeTestService smokeTestService)
{
    public static bool IsHeadlessCommand(CommandLineOptions options)
    {
        return options.ListVersions ||
               options.ProvisionJava ||
               options.InstallLoader ||
               options.AnalyzeCrash ||
               options.SearchMods ||
               options.InstallMod ||
               options.ScanMods ||
               options.SmokeTest;
    }

    public async Task<CommandLineExecutionResult> RunAsync(
        CommandLineOptions commandLine,
        CancellationToken cancellationToken = default)
    {
        var paths = runtimeConfiguration.Paths;
        var targetVersion = commandLine.Version ?? runtimeConfiguration.DefaultTargetVersion;

        if (commandLine.ListVersions)
        {
            var catalog = await versionService.GetCatalogAsync(cancellationToken).ConfigureAwait(false);
            await resultWriter.WriteAsync(
                paths.DataRoot,
                "version-catalog.json",
                catalog,
                cancellationToken).ConfigureAwait(false);
            return new CommandLineExecutionResult(true, 0);
        }

        if (commandLine.ProvisionJava)
        {
            if (commandLine.JavaMajor is not { } javaMajor)
            {
                await WriteJavaProvisionResultAsync(
                    new JavaRuntimeProvisionOutcome(false, null, "Java.InvalidMajor"),
                    cancellationToken).ConfigureAwait(false);
                return new CommandLineExecutionResult(true, 1);
            }

            var component = runtimeConfiguration.MojangJavaComponents.GetValueOrDefault(javaMajor);
            var provisionResult = await javaRuntimeProvisioner
                .EnsureAsync(javaMajor, component, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            await WriteJavaProvisionResultAsync(provisionResult, cancellationToken).ConfigureAwait(false);
            return new CommandLineExecutionResult(true, provisionResult.Succeeded ? 0 : 1);
        }

        if (commandLine.InstallLoader)
        {
            if (!Enum.TryParse<LoaderKind>(commandLine.Loader, true, out var loaderKind))
            {
                await resultWriter.WriteAsync(
                    paths.DataRoot,
                    "loader-install-result.json",
                    new LoaderInstallOutcome(false, "Loader.InvalidKind", ErrorDetail: commandLine.Loader),
                    cancellationToken).ConfigureAwait(false);
                return new CommandLineExecutionResult(true, 1);
            }

            var loaderResult = await loaderInstallationService.InstallAsync(
                new LoaderInstallRequest(
                    targetVersion,
                    loaderKind,
                    commandLine.LoaderVersion,
                    commandLine.JavaPath),
                cancellationToken: cancellationToken).ConfigureAwait(false);
            await resultWriter.WriteAsync(
                paths.DataRoot,
                "loader-install-result.json",
                loaderResult,
                cancellationToken).ConfigureAwait(false);
            return new CommandLineExecutionResult(true, loaderResult.Succeeded ? 0 : 1);
        }

        if (commandLine.AnalyzeCrash)
        {
            var crashReport = await crashAnalysisService
                .AnalyzeAsync(targetVersion, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            await resultWriter.WriteAsync(
                paths.DataRoot,
                "crash-analysis-result.json",
                crashReport,
                cancellationToken).ConfigureAwait(false);
            return new CommandLineExecutionResult(true, 0);
        }

        if (commandLine.SearchMods || commandLine.InstallMod || commandLine.ScanMods)
        {
            var loaderKind = Enum.TryParse<LoaderKind>(commandLine.Loader, true, out var parsedLoader)
                ? parsedLoader
                : LoaderKind.Vanilla;
            if (commandLine.SearchMods)
            {
                var results = await modService.SearchAsync(new ModSearchQuery(
                    commandLine.Query ?? string.Empty,
                    targetVersion,
                    loaderKind), cancellationToken).ConfigureAwait(false);
                await resultWriter.WriteAsync(
                    paths.DataRoot,
                    "mod-command-result.json",
                    new { Kind = "search", Results = results },
                    cancellationToken).ConfigureAwait(false);
                return new CommandLineExecutionResult(true, 0);
            }

            if (commandLine.ScanMods)
            {
                var mods = await modService.ScanAsync(targetVersion, cancellationToken).ConfigureAwait(false);
                var conflicts = await modService
                    .DetectConflictsAsync(targetVersion, cancellationToken)
                    .ConfigureAwait(false);
                await resultWriter.WriteAsync(
                    paths.DataRoot,
                    "mod-command-result.json",
                    new { Kind = "scan", Mods = mods, Conflicts = conflicts },
                    cancellationToken).ConfigureAwait(false);
                return new CommandLineExecutionResult(true, 0);
            }

            if (!Enum.TryParse<ModProvider>(commandLine.Provider, true, out var provider) ||
                string.IsNullOrWhiteSpace(commandLine.ProjectId))
            {
                await resultWriter.WriteAsync(
                    paths.DataRoot,
                    "mod-command-result.json",
                    new ModInstallOutcome(false, "Mods.InvalidRequest"),
                    cancellationToken).ConfigureAwait(false);
                return new CommandLineExecutionResult(true, 1);
            }

            var installResult = await modService.InstallRemoteAsync(
                new ModInstallRequest(
                    provider,
                    commandLine.ProjectId,
                    targetVersion,
                    loaderKind),
                cancellationToken: cancellationToken).ConfigureAwait(false);
            await resultWriter.WriteAsync(
                paths.DataRoot,
                "mod-command-result.json",
                installResult,
                cancellationToken).ConfigureAwait(false);
            return new CommandLineExecutionResult(true, installResult.Succeeded ? 0 : 1);
        }

        if (commandLine.SmokeTest)
        {
            var result = await smokeTestService.RunAsync(
                new SmokeTestRequest(
                    paths.GameRoot,
                    targetVersion,
                    commandLine.JavaPath),
                cancellationToken: cancellationToken).ConfigureAwait(false);
            await resultWriter.WriteAsync(
                paths.DataRoot,
                "smoke-result.json",
                new
                {
                    result.Succeeded,
                    result.TargetVersion,
                    result.FinalStage,
                    result.MessageKey,
                    result.Duration,
                    result.ExitCode,
                    result.Warnings,
                    result.ErrorDetail,
                    ProgressCount = result.Progress.Count,
                    LastProgress = result.Progress.LastOrDefault()
                },
                cancellationToken).ConfigureAwait(false);
            return new CommandLineExecutionResult(true, result.Succeeded ? 0 : 1);
        }

        return new CommandLineExecutionResult(false, 0);
    }

    private Task WriteJavaProvisionResultAsync(
        JavaRuntimeProvisionOutcome result,
        CancellationToken cancellationToken)
    {
        return resultWriter.WriteAsync(
            runtimeConfiguration.Paths.DataRoot,
            "java-provision-result.json",
            result,
            cancellationToken);
    }
}
