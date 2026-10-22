using Serilog;

namespace SlcMinecraftLaunch.Infrastructure.Logging;

public sealed class SerilogAppLogger : SlcMinecraftLaunch.Domain.Abstractions.IAppLogger, IDisposable
{
    private readonly ILogger _logger;

    private SerilogAppLogger(string logDirectory, ILogger logger)
    {
        LogDirectory = logDirectory;
        _logger = logger;
    }

    public string LogDirectory { get; }

    public void Information(string message)
    {
        _logger.Information("{Message}", LogRedactor.Redact(message));
    }

    public static SerilogAppLogger Create(string dataRoot)
    {
        var logDirectory = Path.Combine(dataRoot, "logs");
        Directory.CreateDirectory(logDirectory);
        var logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(logDirectory, "slc-.log"),
                rollingInterval: RollingInterval.Day,
                fileSizeLimitBytes: 10 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                retainedFileCountLimit: 7,
                shared: true)
            .CreateLogger();
        return new SerilogAppLogger(logDirectory, logger);
    }

    public void Warning(string message)
    {
        _logger.Warning("{Message}", LogRedactor.Redact(message));
    }

    public void Error(string message, Exception? exception = null)
    {
        if (exception is null)
        {
            _logger.Error("{Message}", LogRedactor.Redact(message));
            return;
        }

        _logger.Error(
            "{Message}{NewLine}{ExceptionText}",
            LogRedactor.Redact(message),
            LogRedactor.Redact(exception.ToString()));
    }

    public void Dispose()
    {
        (_logger as IDisposable)?.Dispose();
    }
}
