namespace SlcMinecraftLaunch.Domain.Models;

public sealed record ModSearchItem(
    ModProvider Provider,
    string ProjectId,
    string Name,
    string? Description,
    string? Author,
    string? IconUrl,
    long? Downloads);

public sealed record ModSearchOutcome(
    IReadOnlyList<ModSearchItem> Items,
    IReadOnlyList<string> Warnings);

