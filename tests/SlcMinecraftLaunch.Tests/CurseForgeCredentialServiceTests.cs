using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;
using SlcMinecraftLaunch.Infrastructure.Mods;
using SlcMinecraftLaunch.Infrastructure.Persistence;
using SlcMinecraftLaunch.Infrastructure.Security;
using System.Net;

namespace SlcMinecraftLaunch.Tests;

public sealed class CurseForgeCredentialServiceTests
{
    [Fact]
    public async Task SaveAndLoad_RoundTripsWithoutWritingPlaintext()
    {
        var root = Path.Combine(Path.GetTempPath(), $"slc-curseforge-{Guid.NewGuid():N}");
        try
        {
            var store = new JsonCurseForgeCredentialStore(root, new TokenProtector());
            var firstState = new ModApiCredentialState();
            var firstService = new CurseForgeCredentialService(store, firstState);

            var saved = await firstService.SaveApiKeyAsync("  cf-test-secret  ");

            Assert.True(saved.IsConfigured);
            Assert.True(firstService.IsConfigured);
            Assert.True(File.Exists(store.CredentialPath));
            Assert.DoesNotContain(
                "cf-test-secret",
                await File.ReadAllTextAsync(store.CredentialPath));

            var secondState = new ModApiCredentialState();
            var secondService = new CurseForgeCredentialService(store, secondState);
            var loaded = await secondService.LoadStatusAsync();

            Assert.True(loaded.IsConfigured);
            Assert.True(secondService.IsConfigured);
            // PROJBOBCAT: The provider exposes the key through its public
            // ILauncherCoreSettingsProvider contract.
            Assert.Equal(
                "cf-test-secret",
                new ModApiSettingsProvider(
                    new ModApiOptions("https://example.invalid/modrinth", "https://example.invalid/curseforge"),
                    secondState).CurseForgeApiKey);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public async Task Clear_RemovesCiphertextAndDisablesProvider()
    {
        var root = Path.Combine(Path.GetTempPath(), $"slc-curseforge-{Guid.NewGuid():N}");
        try
        {
            var store = new JsonCurseForgeCredentialStore(root, new TokenProtector());
            var state = new ModApiCredentialState();
            var service = new CurseForgeCredentialService(store, state);
            await service.SaveApiKeyAsync("cf-test-secret");

            var cleared = await service.ClearApiKeyAsync();

            Assert.False(cleared.IsConfigured);
            Assert.False(service.IsConfigured);
            // PROJBOBCAT: Clearing the credential must update the provider.
            Assert.Empty(
                new ModApiSettingsProvider(
                    new ModApiOptions("https://example.invalid/modrinth", "https://example.invalid/curseforge"),
                    state).CurseForgeApiKey);
            Assert.False(File.Exists(store.CredentialPath));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public async Task Load_RejectsCorruptedDocumentWithoutThrowing()
    {
        var root = Path.Combine(Path.GetTempPath(), $"slc-curseforge-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(root);
            var store = new JsonCurseForgeCredentialStore(root, new TokenProtector());
            await File.WriteAllTextAsync(
                store.CredentialPath,
                """{"Version":1,"ProtectedPayload":""");
            var service = new CurseForgeCredentialService(store, new ModApiCredentialState());

            var status = await service.LoadStatusAsync();

            Assert.False(status.IsConfigured);
            Assert.Equal("Settings.CurseForgeKeyUnreadable", status.MessageKey);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Save_RejectsEmptyKey(string apiKey)
    {
        var root = Path.Combine(Path.GetTempPath(), $"slc-curseforge-{Guid.NewGuid():N}");
        try
        {
            var service = new CurseForgeCredentialService(
                new JsonCurseForgeCredentialStore(root, new TokenProtector()),
                new ModApiCredentialState());

            var status = await service.SaveApiKeyAsync(apiKey);

            Assert.False(status.IsConfigured);
            Assert.Equal("Settings.CurseForgeKeyRequired", status.MessageKey);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public async Task AuthenticationHandler_UsesLatestKeyAfterReplaceAndClear()
    {
        var root = Path.Combine(Path.GetTempPath(), $"slc-curseforge-{Guid.NewGuid():N}");
        try
        {
            var state = new ModApiCredentialState();
            var service = new CurseForgeCredentialService(
                new JsonCurseForgeCredentialStore(root, new TokenProtector()),
                state);
            var capture = new CapturingHandler();
            using var client = new HttpClient(
                new CurseForgeAuthenticationHandler(state)
                {
                    InnerHandler = capture
                });

            await service.SaveApiKeyAsync("cf-first-key");
            using var first = await client.GetAsync("https://example.invalid/first");
            Assert.Equal("cf-first-key", capture.LastApiKey);

            await service.SaveApiKeyAsync("cf-second-key");
            using var second = await client.GetAsync("https://example.invalid/second");
            Assert.Equal("cf-second-key", capture.LastApiKey);

            await service.ClearApiKeyAsync();
            using var third = await client.GetAsync("https://example.invalid/third");
            Assert.Null(capture.LastApiKey);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? LastApiKey { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastApiKey = request.Headers.TryGetValues("x-api-key", out var values)
                ? values.SingleOrDefault()
                : null;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
