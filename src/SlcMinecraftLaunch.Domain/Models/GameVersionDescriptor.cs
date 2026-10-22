namespace SlcMinecraftLaunch.Domain.Models;

public sealed record GameVersionDescriptor(
    string Id,
    string DisplayName,
    string BaseVersion,
    LoaderKind LoaderKind,
    string? LoaderVersion,
    int? JavaMajorVersion,
    string? JavaComponent,
    bool IsBroken);

