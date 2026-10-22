using System.Collections.Concurrent;
using System.Diagnostics;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Application;

public sealed class SmokeTestService(
    IGameVersionMaterializer versionMaterializer,
    IJavaDiscovery javaDiscovery,
    IGameRuntimeProvider runtimeProvider,
    JavaSelectionPolicy javaSelectionPolicy) : ISmokeTestService
{
    public async Task<SmokeTestResult> RunAsync(
        SmokeTestRequest request,
        IProgress<OperationProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TargetVersion);

        var stopwatch = Stopwatch.StartNew();
        var reports = new ConcurrentQueue<OperationProgress>();
        var progressSink = new InlineProgress<OperationProgress>(value =>
        {
            reports.Enqueue(value);
            progress?.Report(value);
        });

        void Report(SmokeTestStage stage, string messageKey, double fraction, string? detail = null)
        {
            progressSink.Report(new OperationProgress(
                stage,
                messageKey,
                OperationProgress.NormalizeFraction(fraction),
                Detail: detail));
        }

        try
        {
            Report(SmokeTestStage.MaterializingVersion, "Progress.Materializing", 0.02);
            await versionMaterializer.EnsureVersionAsync(
                request.RootPath,
                request.TargetVersion,
                progressSink,
                cancellationToken).ConfigureAwait(false);

            Report(SmokeTestStage.DetectingJava, "Progress.DetectingJava", 0.12);
            var candidates = await javaDiscovery.DiscoverAsync(cancellationToken).ConfigureAwait(false);

            JavaCandidate? explicitCandidate = null;
            if (!string.IsNullOrWhiteSpace(request.ExplicitJavaPath))
            {
                explicitCandidate = await javaDiscovery
                    .InspectAsync(request.ExplicitJavaPath, cancellationToken)
                    .ConfigureAwait(false);

                if (explicitCandidate is not null)
                {
                    candidates = candidates
                        .Append(explicitCandidate)
                        .DistinctBy(candidate => candidate.ExecutablePath, StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                }
            }

            var selection = javaSelectionPolicy.Select(
                candidates,
                request.TargetVersion,
                LoaderKind.Vanilla,
                explicitCandidate?.ExecutablePath);

            if (selection.Selected is null)
            {
                return Failure(
                    "Smoke.NoCompatibleJava",
                    "No compatible Java runtime was found.");
            }

            Report(
                SmokeTestStage.SelectingJava,
                "Progress.JavaSelected",
                0.18,
                selection.Selected.ExecutablePath);

            var runtime = await runtimeProvider.GetAsync(cancellationToken).ConfigureAwait(false);
            Report(SmokeTestStage.ResolvingVersion, "Progress.ResolvingVersion", 0.22);

            var snapshot = await runtime.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
            if (!snapshot.VersionIds.Contains(request.TargetVersion, StringComparer.OrdinalIgnoreCase))
            {
                return Failure(
                    "Smoke.VersionNotResolved",
                    $"The locator could not resolve version '{request.TargetVersion}'.");
            }

            Report(SmokeTestStage.CompletingResources, "Progress.CompletingResources", 0.25);
            var launchOutcome = await runtime.LaunchSmokeAsync(
                new SmokeLaunchRequest(
                    request.TargetVersion,
                    selection.Selected.ExecutablePath,
                    request.OfflineUsername,
                    request.EffectiveHealthCheckDuration),
                progressSink,
                cancellationToken).ConfigureAwait(false);

            stopwatch.Stop();
            var warnings = selection.Warnings
                .Select(warning => $"Java.{warning}")
                .Concat(launchOutcome.Warnings ?? Array.Empty<string>())
                .ToArray();

            return new SmokeTestResult(
                launchOutcome.Succeeded,
                request.TargetVersion,
                launchOutcome.Succeeded ? SmokeTestStage.Completed : SmokeTestStage.Failed,
                launchOutcome.MessageKey,
                stopwatch.Elapsed,
                launchOutcome.ExitCode,
                warnings,
                reports.ToArray(),
                launchOutcome.ErrorDetail);

            SmokeTestResult Failure(string messageKey, string detail)
            {
                stopwatch.Stop();
                Report(SmokeTestStage.Failed, messageKey, 1, detail);
                return new SmokeTestResult(
                    false,
                    request.TargetVersion,
                    SmokeTestStage.Failed,
                    messageKey,
                    stopwatch.Elapsed,
                    null,
                    selection.Warnings.Select(warning => $"Java.{warning}").ToArray(),
                    reports.ToArray(),
                    detail);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            Report(SmokeTestStage.Cancelled, "Smoke.Cancelled", 1);
            return new SmokeTestResult(
                false,
                request.TargetVersion,
                SmokeTestStage.Cancelled,
                "Smoke.Cancelled",
                stopwatch.Elapsed,
                null,
                Array.Empty<string>(),
                reports.ToArray());
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            Report(SmokeTestStage.Failed, "Smoke.UnexpectedError", 1, exception.Message);
            return new SmokeTestResult(
                false,
                request.TargetVersion,
                SmokeTestStage.Failed,
                "Smoke.UnexpectedError",
                stopwatch.Elapsed,
                null,
                Array.Empty<string>(),
                reports.ToArray(),
                exception.ToString());
        }
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value)
        {
            report(value);
        }
    }
}
