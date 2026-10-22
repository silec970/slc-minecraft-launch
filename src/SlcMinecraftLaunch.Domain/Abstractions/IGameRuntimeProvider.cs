namespace SlcMinecraftLaunch.Domain.Abstractions;

public interface IGameRuntimeProvider
{
    Task<IGameRuntime> GetAsync(CancellationToken cancellationToken = default);
}

