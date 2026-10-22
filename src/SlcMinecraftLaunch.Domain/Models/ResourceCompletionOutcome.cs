namespace SlcMinecraftLaunch.Domain.Models;

public sealed record ResourceCompletionOutcome(
    bool Succeeded,
    string MessageKey,
    string? ErrorDetail = null,
    int FailedFileCount = 0);

