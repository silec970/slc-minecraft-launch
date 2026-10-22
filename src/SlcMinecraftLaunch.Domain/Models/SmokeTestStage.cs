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
    DownloadingMod,
    Authenticating,
    Launching,
    VerifyingProcess,
    Completed,
    Failed,
    Cancelled
}

