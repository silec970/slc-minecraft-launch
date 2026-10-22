namespace SlcMinecraftLaunch.Domain.Abstractions;

public interface IDiagnosticBundleService
{
    Task<string> CreateAsync(
        string destinationPath,
        CancellationToken cancellationToken = default);
}
