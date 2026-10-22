namespace SlcMinecraftLaunch.Domain.Models;

public enum SmokeTestStage
{
    Preparing,
    MaterializingVersion,
    DetectingJava,
    SelectingJava,
    DownloadingJava,
    ResolvingVersion,
    CompletingResources,
    InstallingLoader,
    Authenticating,
    Launching,
    VerifyingProcess,
    Completed,
    Failed,
    Cancelled
}

