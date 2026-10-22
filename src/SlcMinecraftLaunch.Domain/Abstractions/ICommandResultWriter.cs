namespace SlcMinecraftLaunch.Domain.Abstractions;

public interface ICommandResultWriter
{
    Task WriteAsync(
        string dataRoot,
        string fileName,
        object value,
        CancellationToken cancellationToken = default);
}
