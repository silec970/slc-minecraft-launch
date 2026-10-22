using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Domain.Abstractions;

public interface IJavaDiscovery
{
    Task<IReadOnlyList<JavaCandidate>> DiscoverAsync(CancellationToken cancellationToken = default);

    Task<JavaCandidate?> InspectAsync(string executablePath, CancellationToken cancellationToken = default);
}

