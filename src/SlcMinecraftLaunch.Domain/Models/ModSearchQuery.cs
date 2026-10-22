namespace SlcMinecraftLaunch.Domain.Models;

public sealed record ModSearchQuery(
    string SearchText,
    string MinecraftVersion,
    LoaderKind LoaderKind,
    int Limit = 20);

