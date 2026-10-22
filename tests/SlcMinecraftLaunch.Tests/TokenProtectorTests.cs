using System.Security.Cryptography;
using System.Text;
using SlcMinecraftLaunch.Infrastructure.Security;

namespace SlcMinecraftLaunch.Tests;

public sealed class TokenProtectorTests
{
    [Fact]
    public void ProtectAndUnprotect_RoundTripsForCurrentUser()
    {
        var protector = new TokenProtector();
        var plaintext = Encoding.UTF8.GetBytes("cf-test-secret");

        var protectedPayload = protector.Protect(plaintext);
        var restored = protector.Unprotect(protectedPayload);

        Assert.Equal(plaintext, restored);
        Assert.NotEqual(plaintext, protectedPayload);
    }

    [Fact]
    public void Unprotect_RejectsDifferentEntropy()
    {
        var source = new TokenProtector(Encoding.UTF8.GetBytes("source-entropy"));
        var other = new TokenProtector(Encoding.UTF8.GetBytes("other-entropy"));
        var protectedPayload = source.Protect(Encoding.UTF8.GetBytes("cf-test-secret"));

        Assert.ThrowsAny<CryptographicException>(() => other.Unprotect(protectedPayload));
    }

    [Fact]
    public void Unprotect_RejectsCorruptedCiphertext()
    {
        var protector = new TokenProtector();
        var protectedPayload = protector.Protect(Encoding.UTF8.GetBytes("cf-test-secret"));
        protectedPayload[protectedPayload.Length / 2] ^= 0x5A;

        Assert.ThrowsAny<CryptographicException>(() => protector.Unprotect(protectedPayload));
    }
}
