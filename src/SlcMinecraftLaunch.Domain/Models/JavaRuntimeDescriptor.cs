namespace SlcMinecraftLaunch.Domain.Models;

public sealed record JavaRuntimeDescriptor(
    JavaCandidate Candidate,
    string CompatibilityKey,
    string SourceKey,
    string IntegrityKey,
    bool IsManualOverride)
{
    public string ExecutablePath => Candidate.ExecutablePath;
    public int MajorVersion => Candidate.MajorVersion;
    public bool CanReverify => Candidate.IsManaged;
    public bool CanDelete => Candidate.IsManaged;
}
