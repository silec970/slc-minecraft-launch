using System.Net;
using System.Security.Cryptography;
using System.Text;
using SlcMinecraftLaunch.Domain.Models;
using SlcMinecraftLaunch.Infrastructure.Loaders;

namespace SlcMinecraftLaunch.Tests;

public sealed class RemoteArtifactHashVerifierTests
{
    [Fact]
    public async Task Verify_AcceptsMatchingSha1Sidecar()
    {
        var root = Path.Combine(Path.GetTempPath(), $"slc-hash-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var file = Path.Combine(root, "artifact.jar");
        var content = Encoding.UTF8.GetBytes("verified artifact");
        await File.WriteAllBytesAsync(file, content);
        var hash = Convert.ToHexString(SHA1.HashData(content));
        var verifier = new RemoteArtifactHashVerifier(
            new StaticFactory(new Handler(hash)));

        try
        {
            var result = await verifier.VerifyAsync(
                "https://example.invalid/artifact.jar",
                file);

            Assert.Equal(ArtifactVerificationStatus.Verified, result.Status);
            Assert.Equal("SHA1", result.Algorithm);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Verify_RejectsNeoForgeInstallerWithMismatchedSha1()
    {
        var root = Path.Combine(Path.GetTempPath(), $"slc-neoforge-hash-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var file = Path.Combine(root, "neoforge-21.1.80-installer.jar");
        await File.WriteAllTextAsync(file, "neoform-installer");
        var verifier = new RemoteArtifactHashVerifier(
            new StaticFactory(new Handler(new string('0', 40))));

        try
        {
            var result = await verifier.VerifyAsync(
                "https://maven.neoforged.net/releases/net/neoforged/neoforge/21.1.80/neoforge-21.1.80-installer.jar",
                file);

            Assert.Equal(ArtifactVerificationStatus.Mismatch, result.Status);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private sealed class StaticFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class Handler(string hash) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith(".sha1", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(hash)
                });
            }

            if (request.RequestUri.AbsolutePath.EndsWith(".sha256", StringComparison.Ordinal))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([])
            });
        }
    }
}
