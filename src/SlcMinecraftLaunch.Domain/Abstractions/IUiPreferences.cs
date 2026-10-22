using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Domain.Abstractions;

public interface IUiPreferences
{
    UiSettings Current { get; }

    Task ApplyAsync(UiSettings settings, CancellationToken cancellationToken = default);
}

