namespace SlcMinecraftLaunch.Domain.Models;

public enum SmokeTestStage
{
    Preparing,
    MaterializingVersion,
    DetectingJava,
    SelectingJava,
    ResolvingVersion,
    CompletingResources,
    Authenticating,
    Launching,
    VerifyingProcess,
    Completed,
    Failed,
    Cancelled
}

