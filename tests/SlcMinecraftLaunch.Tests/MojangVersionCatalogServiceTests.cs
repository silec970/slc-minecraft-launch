using System.Net;
using System.Text;
using SlcMinecraftLaunch.Infrastructure.Mojang;

namespace SlcMinecraftLaunch.Tests;

public sealed class MojangVersionCatalogServiceTests
{
    [Fact]
    public async Task GetVersions_ReadsTypesAndLatestFlags()
    {
        var json = """
                   {
                     "latest": { "release": "1.21.4", "snapshot": "26.3" },
                     "versions": [
                       {
                         "id": "26.3",
                         "type": "snapshot",
                         "releaseTime": "2026-10-01T10:00:00Z"
                       },
                       {
                         "id": "1.21.4",
                         "type": "release",
                         "releaseTime": "2026-09-01T10:00:00Z"
                       }
                     ]
                   }
                   """;
        var service = new MojangVersionCatalogService(
            new FakeHttpClientFactory(json),
            "https://example.invalid/manifest.json");

        var versions = await service.GetVersionsAsync();

        Assert.Equal(["26.3", "1.21.4"], versions.Select(version => version.Id));
        Assert.True(versions[0].IsLatestSnapshot);
        Assert.True(versions[1].IsLatestRelease);
    }

    private sealed class FakeHttpClientFactory(string content) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new Handler(content));

        private sealed class Handler(string content) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(content, Encoding.UTF8, "application/json")
                });
            }
        }
    }
}

