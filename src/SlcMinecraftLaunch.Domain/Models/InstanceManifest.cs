namespace SlcMinecraftLaunch.Domain.Models;

public sealed record InstanceManifest(
    string Id,
    string Name,
    string MinecraftVersion,
    LoaderKind LoaderKind,
    string? LoaderVersion,
    InstanceLaunchSettings LaunchSettings,
    bool IsPlaceholder,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record GameInstance(
    string Id,
    string Name,
    string MinecraftVersion,
    LoaderKind LoaderKind,
    string? LoaderVersion,
    bool IsFavorite,
    bool IsIsolated,
    bool IsPlaceholder,
    InstanceLaunchSettings LaunchSettings);

public sealed record NewInstanceRequest(
    string Id,
    string Name,
    string MinecraftVersion,
    LoaderKind LoaderKind,
    string? LoaderVersion = null);

public sealed record InstanceOperationOutcome(
    bool Succeeded,
    string MessageKey,
    string? InstanceId = null,
    string? ErrorDetail = null,
    IReadOnlyList<string>? Warnings = null);
