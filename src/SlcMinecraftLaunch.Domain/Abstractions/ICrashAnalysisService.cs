using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Domain.Abstractions;

public interface ICrashAnalysisService
{
    Task<CrashAnalysisReport> AnalyzeAsync(
        string versionId,
        int? exitCode = null,
        CancellationToken cancellationToken = default);
}

