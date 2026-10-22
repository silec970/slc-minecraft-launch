namespace SlcMinecraftLaunch.Domain.Abstractions;

public interface IAppLogger
{
    void Warning(string message);

    void Error(string message, Exception? exception = null);
}
