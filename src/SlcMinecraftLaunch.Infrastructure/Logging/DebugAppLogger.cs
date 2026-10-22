using System.Diagnostics;
using SlcMinecraftLaunch.Domain.Abstractions;

namespace SlcMinecraftLaunch.Infrastructure.Logging;

public sealed class DebugAppLogger : IAppLogger
{
    public void Warning(string message)
    {
        Debug.WriteLine($"[WARN] {message}");
    }

    public void Error(string message, Exception? exception = null)
    {
        Debug.WriteLine($"[ERROR] {message}");
        if (exception is not null)
        {
            Debug.WriteLine(exception);
        }
    }
}
