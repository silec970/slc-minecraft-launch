using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Domain.Abstractions;

public interface ICrashAnalysisBackend
{
    Task<IReadOnlyList<CrashInsight>> AnalyzeAsync(
        CrashAnalysisRequest request,
        CancellationToken cancellationToken = default);
}

