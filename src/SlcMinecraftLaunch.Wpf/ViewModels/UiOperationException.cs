namespace SlcMinecraftLaunch.Wpf.ViewModels;

public sealed class UiOperationException(string messageKey) : Exception(messageKey)
{
    public string MessageKey { get; } = messageKey;
}

