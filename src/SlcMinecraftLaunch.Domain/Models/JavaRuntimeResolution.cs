namespace SlcMinecraftLaunch.Domain.Models;

public sealed record JavaRuntimeResolution(
    JavaCandidate? Selected,
    IReadOnlyList<JavaSelectionWarning> Warnings,
    bool WasProvisioned,
    string MessageKey,
    string? ErrorDetail = null);

