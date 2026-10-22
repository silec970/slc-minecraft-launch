using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Application;

public sealed class ModConflictAnalyzer(
    IReadOnlyList<KnownModConflict>? knownConflicts = null)
{
    private readonly IReadOnlyList<KnownModConflict> _knownConflicts =
        knownConflicts ?? Array.Empty<KnownModConflict>();

    public IReadOnlyList<ModConflict> Analyze(
        IReadOnlyList<ModEntry> mods,
        LoaderKind? targetLoader = null)
    {
        var conflicts = new List<ModConflict>();
        var byId = mods
            .GroupBy(mod => mod.Metadata.ModId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        var ids = byId.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var duplicate in mods
                     .GroupBy(mod => mod.Metadata.ModId, StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Count() > 1))
        {
            conflicts.Add(new ModConflict(
                ModConflictKind.DuplicateModId,
                "Mods.Conflict.Duplicate",
                duplicate.Key,
                null,
                duplicate.Select(mod => mod.FileName).ToArray()));
        }

        foreach (var mod in mods)
        {
            if (targetLoader is { } loader &&
                mod.Metadata.LoaderKind != LoaderKind.Other &&
                mod.Metadata.LoaderKind != loader)
            {
                conflicts.Add(new ModConflict(
                    ModConflictKind.LoaderMismatch,
                    "Mods.Conflict.LoaderMismatch",
                    mod.Metadata.ModId,
                    loader.ToString(),
                    [mod.FileName]));
            }

            foreach (var requirement in mod.Metadata.Requirements.Where(item => item.IsMandatory))
            {
                if (!byId.TryGetValue(requirement.ModId, out var installed))
                {
                    conflicts.Add(new ModConflict(
                        ModConflictKind.MissingDependency,
                        "Mods.Conflict.MissingDependency",
                        mod.Metadata.ModId,
                        requirement.ModId,
                        [mod.FileName]));
                    continue;
                }

                var ranges = requirement.VersionRanges is { Count: > 0 }
                    ? requirement.VersionRanges
                    : requirement.VersionRange is { Length: > 0 }
                        ? [requirement.VersionRange]
                        : null;
                if (ranges is not null &&
                    !installed.Any(candidate =>
                        MavenVersionRange.AnySatisfied(ranges, candidate.Metadata.Version)))
                {
                    conflicts.Add(new ModConflict(
                        ModConflictKind.IncompatibleDependencyVersion,
                        "Mods.Conflict.IncompatibleDependencyVersion",
                        mod.Metadata.ModId,
                        requirement.ModId,
                        [mod.FileName]));
                }
            }
        }

        foreach (var dependencyGroup in mods
                     .SelectMany(mod => mod.Metadata.Requirements
                         .Where(requirement => requirement.IsMandatory)
                         .Select(requirement => (Parent: mod, Requirement: requirement)))
                     .GroupBy(item => item.Requirement.ModId, StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Select(item => item.Parent.Metadata.ModId)
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .Count() > 1))
        {
            var rangeGroups = dependencyGroup
                .Select(item => item.Requirement.VersionRanges
                                ?? (item.Requirement.VersionRange is { Length: > 0 }
                                    ? [item.Requirement.VersionRange]
                                    : null))
                .Where(ranges => ranges is { Count: > 0 })
                .ToArray();
            if (rangeGroups.Length < 2 ||
                RangesHaveIntersection(rangeGroups))
            {
                continue;
            }

            conflicts.Add(new ModConflict(
                ModConflictKind.DiamondDependencyConflict,
                "Mods.Conflict.DiamondDependency",
                dependencyGroup.Key,
                null,
                dependencyGroup.Select(item => item.Parent.FileName).Distinct().ToArray()));
        }

        foreach (var knownConflict in _knownConflicts)
        {
            if (ids.Contains(knownConflict.FirstModId) && ids.Contains(knownConflict.SecondModId))
            {
                conflicts.Add(new ModConflict(
                    ModConflictKind.KnownIncompatibility,
                    knownConflict.MessageKey,
                    knownConflict.FirstModId,
                    knownConflict.SecondModId,
                    null));
            }
        }

        return conflicts;
    }

    private static bool RangesHaveIntersection(
        IReadOnlyList<IReadOnlyList<string>?> rangeGroups)
    {
        foreach (var left in rangeGroups[0] ?? [])
        {
            var intersects = true;
            for (var index = 1; index < rangeGroups.Count && intersects; index++)
            {
                intersects = rangeGroups[index]?.Any(right =>
                    MavenVersionRange.HaveIntersection(left, right)) == true;
            }

            if (intersects)
            {
                return true;
            }
        }

        return false;
    }
}

