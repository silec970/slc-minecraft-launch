namespace SlcMinecraftLaunch.Domain.Models;

public enum ModConflictKind
{
    DuplicateModId,
    MissingDependency,
    KnownIncompatibility
}

public sealed record ModConflict(
    ModConflictKind Kind,
    string MessageKey,
    string? FirstModId,
    string? SecondModId,
    IReadOnlyList<string>? Files);

public sealed record KnownModConflict(
    string FirstModId,
    string SecondModId,
    string MessageKey);

