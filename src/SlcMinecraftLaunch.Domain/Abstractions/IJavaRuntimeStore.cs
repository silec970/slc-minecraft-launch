using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Domain.Abstractions;

public interface IJavaRuntimeStore
{
    Task<IReadOnlyList<JavaCandidate>> DiscoverAsync(
        CancellationToken cancellationToken = default);

    Task<JavaCandidate?> ReverifyAsync(
        string executablePath,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteManagedAsync(
        string executablePath,
        CancellationToken cancellationToken = default);
}
