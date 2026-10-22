using SlcMinecraftLaunch.Domain.Abstractions;
using SlcMinecraftLaunch.Domain.Models;
using SlcMinecraftLaunch.Infrastructure.Mods;

namespace SlcMinecraftLaunch.Tests;

public sealed class ModProviderCatalogTests
{
    [Fact]
    public async Task Search_WithoutCurseForgeKey_ReturnsWarningWithoutThrowing()
    {
        var catalog = new ProjBobcatModProviderCatalog(
            null!,
            null!,
            new FakeCredentialService(false));

        var outcome = await catalog.SearchAsync(
            new ModSearchQuery("sodium", "1.21.4", LoaderKind.Fabric));

        Assert.Contains("Mods.CurseForgeKeyMissing", outcome.Warnings);
    }

    [Fact]
    public async Task ResolveCurseForge_WithoutKey_ReturnsNoCandidate()
    {
        var catalog = new ProjBobcatModProviderCatalog(
            null!,
            null!,
            new FakeCredentialService(false));

        var candidate = await catalog.ResolveDownloadAsync(
            new ModInstallRequest(
                ModProvider.CurseForge,
                "394468",
                "1.21.4",
                LoaderKind.Fabric));

        Assert.Null(candidate);
    }

    private sealed class FakeCredentialService(bool configured)
        : ICurseForgeCredentialService
    {
        public bool IsConfigured { get; } = configured;

        public Task<CurseForgeCredentialStatus> LoadStatusAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult(new CurseForgeCredentialStatus(IsConfigured, "test"));

        public Task<CurseForgeCredentialStatus> SaveApiKeyAsync(
            string apiKey,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<CurseForgeCredentialStatus> ClearApiKeyAsync(
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
