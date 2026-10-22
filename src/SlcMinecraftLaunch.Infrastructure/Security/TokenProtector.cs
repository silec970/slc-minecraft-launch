using System.Security.Cryptography;
using System.Text;

namespace SlcMinecraftLaunch.Infrastructure.Security;

public sealed class TokenProtector
{
    private static readonly byte[] DefaultEntropy = SHA256.HashData(
        Encoding.UTF8.GetBytes("SlcMinecraftLaunch.CurseForgeApiKey.v1"));

    private readonly byte[] _entropy;

    public TokenProtector(byte[]? entropy = null)
    {
        _entropy = entropy?.ToArray() ?? DefaultEntropy;
        if (_entropy.Length == 0)
        {
            throw new ArgumentException("DPAPI entropy cannot be empty.", nameof(entropy));
        }
    }

    public byte[] Protect(byte[] plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        if (plaintext.Length == 0)
        {
            throw new ArgumentException("The secret cannot be empty.", nameof(plaintext));
        }

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("DPAPI is only available on Windows.");
        }

        return ProtectedData.Protect(
            plaintext,
            _entropy,
            DataProtectionScope.CurrentUser);
    }

    public byte[] Unprotect(byte[] protectedPayload)
    {
        ArgumentNullException.ThrowIfNull(protectedPayload);
        if (protectedPayload.Length == 0)
        {
            throw new ArgumentException("The protected secret cannot be empty.", nameof(protectedPayload));
        }

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("DPAPI is only available on Windows.");
        }

        return ProtectedData.Unprotect(
            protectedPayload,
            _entropy,
            DataProtectionScope.CurrentUser);
    }
}
