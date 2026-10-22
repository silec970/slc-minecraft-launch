namespace SlcMinecraftLaunch.Domain.Models;

public sealed record CrashAnalysisRequest(
    string RootPath,
    string VersionId,
    bool VersionIsolation,
    int? ExitCode = null);

