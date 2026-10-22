using System.Net;
using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;
using SlcMinecraftLaunch.Infrastructure.Configuration;
using SlcMinecraftLaunch.Infrastructure.Instances;
using SlcMinecraftLaunch.Infrastructure.ProjBobcat;

namespace SlcMinecraftLaunch.Tests;

public sealed class InstanceLocatorIntegrationTests
{
    [Fact]
    public async Task MinimalPlaceholder_IsVisibleToDefaultVersionLocator()
    {
        var root = Path.Combine(Path.GetTempPath(), $"slc-instance-locator-{Guid.NewGuid():N}");
        try
        {
            var paths = new PortableLauncherPaths(
                Path.Combine(root, "game"),
                Path.Combine(root, "launcher-data"));
            var storage = new LocalInstanceStorage(paths);
            var manifest = LocalInstanceStorageTests.CreateManifest("locator-empty", true);
            await storage.CreatePlaceholderAsync(manifest);
            await using var provider = new ProjBobcatRuntimeProvider(
                paths,
                new FakeHttpClientFactory(),
                new DownloadSourceOptions([], [], [], [], [], [], [], []),
                new FakeSourceHealthService());

            var runtime = await provider.GetAsync();
            var versions = await runtime.GetVersionsAsync();

            var visible = Assert.Single(versions, version => version.Id == manifest.Id);
            Assert.True(visible.IsBroken);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    private sealed class FakeHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
            => new(new EmptyHandler(), disposeHandler: true);

        private sealed class EmptyHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
                => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private sealed class FakeSourceHealthService : ISourceHealthService
    {
        public Task<IReadOnlyDictionary<string, DownloadSourceScore>> GetScoresAsync(
            IReadOnlyList<string> sourceRoots,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<string, DownloadSourceScore>>(
                new Dictionary<string, DownloadSourceScore>());

        public Task RecordAsync(
            IReadOnlyList<DownloadSourceObservation> observations,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
