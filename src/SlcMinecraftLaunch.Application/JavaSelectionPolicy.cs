using System.Globalization;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Application;

public sealed class JavaSelectionPolicy
{
    public JavaSelectionResult Select(
        IReadOnlyList<JavaCandidate> candidates,
        string minecraftVersion,
        LoaderKind loaderKind,
        string? explicitJavaPath = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitJavaPath))
        {
            var explicitCandidate = candidates.FirstOrDefault(
                candidate => string.Equals(
                    Path.GetFullPath(candidate.ExecutablePath),
                    Path.GetFullPath(explicitJavaPath),
                    StringComparison.OrdinalIgnoreCase));

            return explicitCandidate is null
                ? JavaSelectionResult.None
                : new JavaSelectionResult(
                    explicitCandidate,
                    GetWarnings(explicitCandidate, minecraftVersion, loaderKind)
                        .Append(JavaSelectionWarning.UserOverride)
                        .Distinct()
                        .ToArray());
        }

        var requiredMajor = GetRequiredJavaMajor(minecraftVersion);
        var exact = candidates
            .Where(candidate => candidate.MajorVersion == requiredMajor)
            .OrderBy(candidate => candidate.ExecutablePath, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (exact is not null)
        {
            return new JavaSelectionResult(exact, Array.Empty<JavaSelectionWarning>());
        }

        var newer = candidates
            .Where(candidate => candidate.MajorVersion > requiredMajor)
            .OrderBy(candidate => candidate.MajorVersion)
            .ThenBy(candidate => candidate.ExecutablePath, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (newer is null)
        {
            return JavaSelectionResult.None;
        }

        if (loaderKind is LoaderKind.Forge or LoaderKind.NeoForge)
        {
            return JavaSelectionResult.None;
        }

        if (requiredMajor >= 25)
        {
            return new JavaSelectionResult(newer, Array.Empty<JavaSelectionWarning>());
        }

        return new JavaSelectionResult(
            newer,
            [JavaSelectionWarning.JavaAboveBaseline]);
    }

    public static int GetRequiredJavaMajor(string minecraftVersion)
    {
        if (TryParseVersion(minecraftVersion, out var version) && version.Major >= 26)
        {
            return 25;
        }

        if (version.Major == 1)
        {
            if (version.Minor is >= 12 and <= 16)
            {
                return 8;
            }

            if (version.Minor == 17)
            {
                return 16;
            }

            if (version.Minor is >= 18 and <= 20)
            {
                return version.Minor == 20 && version.Patch >= 5 ? 21 : 17;
            }

            if (version.Minor == 21)
            {
                return 21;
            }
        }

        return 21;
    }

    private static IReadOnlyList<JavaSelectionWarning> GetWarnings(
        JavaCandidate candidate,
        string minecraftVersion,
        LoaderKind loaderKind)
    {
        var requiredMajor = GetRequiredJavaMajor(minecraftVersion);

        if (candidate.MajorVersion > requiredMajor)
        {
            return [JavaSelectionWarning.JavaAboveBaseline];
        }

        return candidate.MajorVersion < requiredMajor
            ? [JavaSelectionWarning.JavaBelowRequirement]
            : Array.Empty<JavaSelectionWarning>();
    }

    private static bool TryParseVersion(string value, out ParsedVersion version)
    {
        var components = value.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (components.Length == 0 ||
            !int.TryParse(components[0], NumberStyles.None, CultureInfo.InvariantCulture, out var major))
        {
            version = default;
            return false;
        }

        var minor = components.Length > 1 &&
                    int.TryParse(components[1], NumberStyles.None, CultureInfo.InvariantCulture, out var parsedMinor)
            ? parsedMinor
            : 0;
        var patch = components.Length > 2 &&
                    int.TryParse(components[2], NumberStyles.None, CultureInfo.InvariantCulture, out var parsedPatch)
            ? parsedPatch
            : 0;

        version = new ParsedVersion(major, minor, patch);
        return true;
    }

    private readonly record struct ParsedVersion(int Major, int Minor, int Patch);
}

