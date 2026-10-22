using ProjBobcat.Interface;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Infrastructure.Configuration;

namespace SlcMinecraftLaunch.Infrastructure.ProjBobcat;

public sealed class ProjBobcatRuntimeProvider(
    PortableLauncherPaths paths,
    IHttpClientFactory httpClientFactory) : IGameRuntimeProvider, IAsyncDisposable
{
    private readonly Lazy<Task<ProjBobcatGameRuntime>> _runtime =
        new(() => ProjBobcatGameRuntime.CreateAsync(paths, httpClientFactory));

    public async Task<IGameRuntime> GetAsync(CancellationToken cancellationToken = default)
    {
        return await _runtime.Value.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_runtime.IsValueCreated)
        {
            await (await _runtime.Value.ConfigureAwait(false)).DisposeAsync().ConfigureAwait(false);
        }
    }
}

