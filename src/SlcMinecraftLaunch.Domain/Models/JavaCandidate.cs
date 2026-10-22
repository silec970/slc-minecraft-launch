namespace SlcMinecraftLaunch.Domain.Models;

public enum JavaRuntimeSource
{
    System,
    Mojang,
    Adoptium
}

public enum JavaIntegrityStatus
{
    NotManaged,
    Recorded,
    Verified,
    Invalid,
    Unknown
}

public sealed record JavaCandidate(
    string ExecutablePath,
    int MajorVersion,
    JavaRuntimeSource Source = JavaRuntimeSource.System,
    JavaIntegrityStatus IntegrityStatus = JavaIntegrityStatus.NotManaged,
    string? RuntimeId = null,
    string? IntegrityDetail = null)
{
    public bool IsManaged => Source is JavaRuntimeSource.Mojang or JavaRuntimeSource.Adoptium;
}

