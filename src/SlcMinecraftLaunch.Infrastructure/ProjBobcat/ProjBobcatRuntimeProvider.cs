using ProjBobcat.Interface;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;
using SlcMinecraftLaunch.Infrastructure.Configuration;

namespace SlcMinecraftLaunch.Infrastructure.ProjBobcat;

public sealed class ProjBobcatRuntimeProvider(
    PortableLauncherPaths paths,
    IHttpClientFactory httpClientFactory,
    DownloadSourceOptions downloadSourceOptions) : IGameRuntimeProvider, IAsyncDisposable
{
    private readonly Lazy<Task<ProjBobcatGameRuntime>> _runtime =
        new(() => ProjBobcatGameRuntime.CreateAsync(paths, httpClientFactory, downloadSourceOptions));

    public async Task<IGameRuntime> GetAsync(CancellationToken cancellationToken = default)
    {
        return await _runtime.Value.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    internal Task<ProjBobcatGameRuntime> GetRuntimeAsync(CancellationToken cancellationToken = default)
    {
        return _runtime.Value.WaitAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_runtime.IsValueCreated)
        {
            await (await _runtime.Value.ConfigureAwait(false)).DisposeAsync().ConfigureAwait(false);
        }
    }
}

