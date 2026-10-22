using System.Diagnostics;
using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using ProjBobcat.Class.Helper;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Infrastructure.ProjBobcat;

[SupportedOSPlatform("windows")]
public sealed partial class ProjBobcatJavaDiscovery : IJavaDiscovery
{
    public async Task<IReadOnlyList<JavaCandidate>> DiscoverAsync(
        CancellationToken cancellationToken = default)
    {
        var discovered = new List<JavaCandidate>();
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // PROJBOBCAT: SystemInfoHelper.FindJava is the mandatory first-pass
        // discovery API across registries, environment variables, and game paths.
        await foreach (var path in SystemInfoHelper.FindJava().WithCancellation(cancellationToken))
        {
            if (!paths.Add(path))
            {
                continue;
            }

            var candidate = await InspectAsync(path, cancellationToken).ConfigureAwait(false);
            if (candidate is not null)
            {
                discovered.Add(candidate);
            }
        }

        return discovered
            .OrderBy(candidate => candidate.MajorVersion)
            .ThenBy(candidate => candidate.ExecutablePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<JavaCandidate?> InspectAsync(
        string executablePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            return null;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));

        var startInfo = new ProcessStartInfo(executablePath)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("-version");

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return null;
        }

        var standardOutput = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var standardError = process.StandardError.ReadToEndAsync(timeout.Token);
        await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);

        var output = string.Concat(
            await standardOutput.ConfigureAwait(false),
            await standardError.ConfigureAwait(false));
        var match = JavaVersionRegex().Match(output);

        if (!match.Success || !int.TryParse(match.Groups["major"].Value, out var major))
        {
            return null;
        }

        return new JavaCandidate(Path.GetFullPath(executablePath), major);
    }

    [GeneratedRegex(@"version\s+""(?<major>\d+)(?:\.(?<minor>\d+))?", RegexOptions.IgnoreCase)]
    private static partial Regex JavaVersionRegex();
}

