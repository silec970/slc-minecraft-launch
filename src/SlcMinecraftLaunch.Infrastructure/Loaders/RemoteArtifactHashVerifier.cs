using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Infrastructure.Loaders;

public sealed partial class RemoteArtifactHashVerifier(
    IHttpClientFactory httpClientFactory)
{
    public async Task<ArtifactVerificationResult> VerifyAsync(
        string artifactUrl,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient();
        var candidates = new List<string> { artifactUrl };

        try
        {
            using var response = await client.GetAsync(
                artifactUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode &&
                response.RequestMessage?.RequestUri is { } finalUri &&
                !string.Equals(finalUri.AbsoluteUri, artifactUrl, StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add(finalUri.AbsoluteUri);
            }
        }
        catch (HttpRequestException)
        {
            // Verification still attempts the original sidecar URLs.
        }

        foreach (var candidate in candidates)
        {
            foreach (var suffix in new[] { ".sha256", ".sha1" })
            {
                var sidecarUrl = candidate + suffix;
                using var sidecarResponse = await client.GetAsync(sidecarUrl, cancellationToken)
                    .ConfigureAwait(false);
                if (sidecarResponse.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
                {
                    continue;
                }

                if (!sidecarResponse.IsSuccessStatusCode)
                {
                    continue;
                }

                var content = await sidecarResponse.Content
                    .ReadAsStringAsync(cancellationToken)
                    .ConfigureAwait(false);
                var expected = HashRegex().Match(content).Value;
                if (string.IsNullOrWhiteSpace(expected))
                {
                    continue;
                }

                var algorithm = expected.Length == 64 ? "SHA256" : "SHA1";
                var actual = await ComputeHashAsync(filePath, algorithm, cancellationToken)
                    .ConfigureAwait(false);
                return string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase)
                    ? new ArtifactVerificationResult(
                        ArtifactVerificationStatus.Verified,
                        algorithm,
                        expected,
                        actual)
                    : new ArtifactVerificationResult(
                        ArtifactVerificationStatus.Mismatch,
                        algorithm,
                        expected,
                        actual,
                        sidecarUrl);
            }
        }

        return new ArtifactVerificationResult(
            ArtifactVerificationStatus.Missing,
            string.Empty,
            null,
            null,
            "No official SHA256 or SHA1 sidecar was available.");
    }

    private static async Task<string> ComputeHashAsync(
        string filePath,
        string algorithm,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        byte[] hash = algorithm == "SHA256"
            ? await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false)
            : await SHA1.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    [GeneratedRegex(@"\b(?:[0-9A-Fa-f]{64}|[0-9A-Fa-f]{40})\b")]
    private static partial Regex HashRegex();
}
