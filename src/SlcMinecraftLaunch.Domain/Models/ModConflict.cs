namespace SlcMinecraftLaunch.Domain.Models;

public enum ModConflictKind
{
    DuplicateModId,
    MissingDependency,
    IncompatibleDependencyVersion,
    DiamondDependencyConflict,
    LoaderMismatch,
    KnownIncompatibility
}

public sealed record ModConflict(
    ModConflictKind Kind,
    string MessageKey,
    string? FirstModId,
    string? SecondModId,
    IReadOnlyList<string>? Files,
    ModProvider? Provider = null,
    string? ProjectId = null,
    string? RequiredVersionId = null);

public sealed record KnownModConflict(
    string FirstModId,
    string SecondModId,
    string MessageKey);

