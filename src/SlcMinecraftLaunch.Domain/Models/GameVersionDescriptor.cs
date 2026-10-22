namespace SlcMinecraftLaunch.Domain.Models;

public sealed record GameVersionDescriptor(
    string Id,
    string DisplayName,
    string BaseVersion,
    LoaderKind LoaderKind,
    string? LoaderVersion,
    int? JavaMajorVersion,
    bool IsBroken);

