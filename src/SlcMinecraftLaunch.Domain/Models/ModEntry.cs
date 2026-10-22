namespace SlcMinecraftLaunch.Domain.Models;

public sealed record ModEntry(
    string FilePath,
    string FileName,
    ModMetadata Metadata,
    long FileSize);

