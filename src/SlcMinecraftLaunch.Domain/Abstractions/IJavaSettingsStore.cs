using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Domain.Abstractions;

public interface IJavaSettingsStore
{
    Task<JavaSettings> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(
        JavaSettings settings,
        CancellationToken cancellationToken = default);
}
