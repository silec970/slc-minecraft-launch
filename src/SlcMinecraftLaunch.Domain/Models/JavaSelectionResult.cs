namespace SlcMinecraftLaunch.Domain.Models;

public enum JavaSelectionWarning
{
    JavaAboveBaseline,
    JavaBelowRequirement,
    UserOverride
}

public sealed record JavaSelectionResult(
    JavaCandidate? Selected,
    IReadOnlyList<JavaSelectionWarning> Warnings)
{
    public static JavaSelectionResult None { get; } =
        new(null, Array.Empty<JavaSelectionWarning>());
}

