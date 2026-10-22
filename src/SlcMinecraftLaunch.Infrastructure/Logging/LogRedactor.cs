using System.Text.RegularExpressions;

namespace SlcMinecraftLaunch.Infrastructure.Logging;

public static partial class LogRedactor
{
    public static string Redact(string value)
    {
        var redacted = AuthorizationRegex().Replace(value, "$1[REDACTED]");
        redacted = TokenRegex().Replace(redacted, "$1$2[REDACTED]");
        redacted = BearerRegex().Replace(redacted, "Bearer [REDACTED]");
        redacted = ApiKeyRegex().Replace(redacted, "$1[REDACTED]");
        return redacted;
    }

    [GeneratedRegex(
        @"(?i)(authorization\s*[:=]\s*)([^\r\n,;]+)",
        RegexOptions.CultureInvariant)]
    private static partial Regex AuthorizationRegex();

    [GeneratedRegex(
        @"(?i)(access[_-]?token|refresh[_-]?token|client[_-]?token|curseforge[_-]?api[_-]?key)([""']?\s*[:=]\s*[""']?)([^\s,;""']+)",
        RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();

    [GeneratedRegex(@"(?i)Bearer\s+[A-Za-z0-9._~+/\-=]+", RegexOptions.CultureInvariant)]
    private static partial Regex BearerRegex();

    [GeneratedRegex(
        @"(?i)(x-api-key\s*[:=]\s*)([^\s,;]+)",
        RegexOptions.CultureInvariant)]
    private static partial Regex ApiKeyRegex();
}
