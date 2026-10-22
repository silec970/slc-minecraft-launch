using System.Globalization;
using System.Text.RegularExpressions;

namespace SlcMinecraftLaunch.Application;

public static partial class MavenVersionRange
{
    public static bool IsSatisfied(string? range, string version)
    {
        if (string.IsNullOrWhiteSpace(range) || range.Trim() == "*")
        {
            return true;
        }

        return range.Split("||", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(clause => IsClauseSatisfied(clause, version));
    }

    public static bool AnySatisfied(IReadOnlyList<string>? ranges, string version)
    {
        return ranges is not { Count: > 0 } ||
               ranges.Any(range => IsSatisfied(range, version));
    }

    public static bool HaveIntersection(string firstRange, string secondRange)
    {
        var first = GetBounds(firstRange);
        var second = GetBounds(secondRange);
        if (first is null || second is null)
        {
            return true;
        }

        var lower = Compare(first.Value.LowerVersion, second.Value.LowerVersion) >= 0
            ? first.Value
            : second.Value;
        var upper = Compare(first.Value.UpperVersion, second.Value.UpperVersion) <= 0
            ? first.Value
            : second.Value;
        var compare = Compare(lower.LowerVersion, upper.UpperVersion);
        return compare < 0 ||
               (compare == 0 && lower.LowerInclusive && upper.UpperInclusive);
    }

    public static int CompareVersions(string left, string right)
    {
        return Compare(left, right);
    }

    private static bool IsClauseSatisfied(string clause, string version)
    {
        if (clause.StartsWith('[') || clause.StartsWith('('))
        {
            var bounds = ParseInterval(clause);
            if (bounds is null)
            {
                return false;
            }

            return Compare(version, bounds.Value.LowerVersion) switch
            {
                > 0 => Compare(version, bounds.Value.UpperVersion) switch
                {
                    < 0 => true,
                    > 0 => false,
                    _ => bounds.Value.UpperInclusive
                },
                < 0 => false,
                _ => bounds.Value.LowerInclusive &&
                     Compare(version, bounds.Value.UpperVersion) < 0
            };
        }

        var comparators = ComparatorRegex().Matches(clause);
        if (comparators.Count == 0)
        {
            return VersionsEqual(version, clause.Trim());
        }

        foreach (Match match in comparators)
        {
            var comparison = Compare(version, match.Groups["version"].Value);
            var operatorText = match.Groups["op"].Value;
            var satisfied = operatorText switch
            {
                ">=" => comparison >= 0,
                ">" => comparison > 0,
                "<=" => comparison <= 0,
                "<" => comparison < 0,
                "=" or "==" => comparison == 0,
                _ => false
            };
            if (!satisfied)
            {
                return false;
            }
        }

        return true;
    }

    private static VersionBounds? GetBounds(string range)
    {
        var clauses = range.Split("||", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return clauses.Length == 1 ? ParseBounds(clauses[0]) : null;
    }

    private static VersionBounds? ParseBounds(string clause)
    {
        if (clause.StartsWith('[') || clause.StartsWith('('))
        {
            return ParseInterval(clause);
        }

        string? lower = null;
        string? upper = null;
        var lowerInclusive = true;
        var upperInclusive = true;

        foreach (Match match in ComparatorRegex().Matches(clause))
        {
            var value = match.Groups["version"].Value;
            switch (match.Groups["op"].Value)
            {
                case ">=":
                    lower = value;
                    lowerInclusive = true;
                    break;
                case ">":
                    lower = value;
                    lowerInclusive = false;
                    break;
                case "<=":
                    upper = value;
                    upperInclusive = true;
                    break;
                case "<":
                    upper = value;
                    upperInclusive = false;
                    break;
                case "=":
                case "==":
                    lower = upper = value;
                    break;
            }
        }

        if (lower is null && upper is null)
        {
            var exact = clause.Trim();
            lower = upper = exact;
        }

        return new VersionBounds(
            lower ?? "0",
            lowerInclusive,
            upper ?? "999999999",
            upperInclusive);
    }

    private static VersionBounds? ParseInterval(string clause)
    {
        if (clause.Length < 3 || !(clause.EndsWith(']') || clause.EndsWith(')')))
        {
            return null;
        }

        var parts = clause[1..^1].Split(',', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            return null;
        }

        return new VersionBounds(
            string.IsNullOrWhiteSpace(parts[0]) ? "0" : parts[0],
            clause[0] == '[',
            string.IsNullOrWhiteSpace(parts[1]) ? "999999999" : parts[1],
            clause[^1] == ']');
    }

    private static bool VersionsEqual(string left, string right)
    {
        return Compare(left, right) == 0;
    }

    private static int Compare(string left, string right)
    {
        var leftParts = Normalize(left);
        var rightParts = Normalize(right);
        var length = Math.Max(leftParts.Count, rightParts.Count);
        for (var index = 0; index < length; index++)
        {
            var leftPart = index < leftParts.Count ? leftParts[index] : "0";
            var rightPart = index < rightParts.Count ? rightParts[index] : "0";
            var leftNumeric = int.TryParse(leftPart, NumberStyles.None, CultureInfo.InvariantCulture, out var leftNumber);
            var rightNumeric = int.TryParse(rightPart, NumberStyles.None, CultureInfo.InvariantCulture, out var rightNumber);
            var comparison = leftNumeric && rightNumeric
                ? leftNumber.CompareTo(rightNumber)
                : string.Compare(leftPart, rightPart, StringComparison.OrdinalIgnoreCase);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return 0;
    }

    private static IReadOnlyList<string> Normalize(string version)
    {
        return version.Trim()
            .TrimStart('v', 'V')
            .Split(['.', '-', '+', '_'], StringSplitOptions.RemoveEmptyEntries);
    }

    [GeneratedRegex(@"(?<op>>=|<=|>|<|==|=)\s*(?<version>[0-9A-Za-z][0-9A-Za-z.+_-]*)")]
    private static partial Regex ComparatorRegex();

    private readonly record struct VersionBounds(
        string LowerVersion,
        bool LowerInclusive,
        string UpperVersion,
        bool UpperInclusive);
}
