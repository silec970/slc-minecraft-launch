using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SlcMinecraftLaunch.Infrastructure.Security;

namespace SlcMinecraftLaunch.Infrastructure.Persistence;

public sealed class JsonCurseForgeCredentialStore
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    private readonly TokenProtector _protector;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public JsonCurseForgeCredentialStore(string dataRoot, TokenProtector protector)
    {
        CredentialPath = Path.Combine(dataRoot, "curseforge-credential.json");
        _protector = protector;
    }

    public string CredentialPath { get; }

    public async Task<string?> LoadApiKeyAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(CredentialPath))
        {
            return null;
        }

        await using var input = File.OpenRead(CredentialPath);
        var document = await JsonSerializer.DeserializeAsync<CredentialDocument>(
                           input,
                           cancellationToken: cancellationToken).ConfigureAwait(false)
                       ?? throw new InvalidDataException("The credential document is empty.");
        if (document.Version != 1 || string.IsNullOrWhiteSpace(document.ProtectedPayload))
        {
            throw new InvalidDataException("The credential document version is unsupported.");
        }

        byte[] protectedPayload;
        try
        {
            protectedPayload = Convert.FromBase64String(document.ProtectedPayload);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("The credential payload is malformed.", exception);
        }

        var plaintext = _protector.Unprotect(protectedPayload);
        try
        {
            return StrictUtf8.GetString(plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
            CryptographicOperations.ZeroMemory(protectedPayload);
        }
    }

    public async Task SaveApiKeyAsync(
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var directory = Path.GetDirectoryName(CredentialPath)
                            ?? throw new InvalidOperationException("The credential path has no directory.");
            Directory.CreateDirectory(directory);
            var plaintext = StrictUtf8.GetBytes(apiKey);
            byte[] protectedPayload;
            try
            {
                protectedPayload = _protector.Protect(plaintext);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }

            try
            {
                var document = new CredentialDocument(
                    1,
                    Convert.ToBase64String(protectedPayload));
                var temporaryPath = $"{CredentialPath}.{Guid.NewGuid():N}.tmp";
                try
                {
                    await using (var output = File.Create(temporaryPath))
                    {
                        await JsonSerializer.SerializeAsync(
                            output,
                            document,
                            new JsonSerializerOptions { WriteIndented = true },
                            cancellationToken).ConfigureAwait(false);
                    }

                    File.Move(temporaryPath, CredentialPath, true);
                }
                finally
                {
                    if (File.Exists(temporaryPath))
                    {
                        File.Delete(temporaryPath);
                    }
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(protectedPayload);
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (File.Exists(CredentialPath))
            {
                File.Delete(CredentialPath);
            }
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private sealed record CredentialDocument(
        int Version,
        string ProtectedPayload);
}
