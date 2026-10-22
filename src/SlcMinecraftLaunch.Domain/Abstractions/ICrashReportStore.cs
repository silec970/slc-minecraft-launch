using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Domain.Abstractions;

public interface ICrashReportStore
{
    Task<string> SaveAsync(
        CrashAnalysisReport report,
        CancellationToken cancellationToken = default);
}
