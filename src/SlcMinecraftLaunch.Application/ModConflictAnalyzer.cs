using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Application;

public sealed class ModConflictAnalyzer(
    IReadOnlyList<KnownModConflict>? knownConflicts = null)
{
    private readonly IReadOnlyList<KnownModConflict> _knownConflicts =
        knownConflicts ?? Array.Empty<KnownModConflict>();

    public IReadOnlyList<ModConflict> Analyze(IReadOnlyList<ModEntry> mods)
    {
        var conflicts = new List<ModConflict>();
        var ids = mods
            .Select(mod => mod.Metadata.ModId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

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
            foreach (var requirement in mod.Metadata.Requirements.Where(item => item.IsMandatory))
            {
                if (!ids.Contains(requirement.ModId))
                {
                    conflicts.Add(new ModConflict(
                        ModConflictKind.MissingDependency,
                        "Mods.Conflict.MissingDependency",
                        mod.Metadata.ModId,
                        requirement.ModId,
                        [mod.FileName]));
                }
            }
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
}

