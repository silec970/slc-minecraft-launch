using SlcMinecraftLaunch.Domain.Models;

namespace SlcMinecraftLaunch.Domain.Abstractions;

public interface IJavaRuntimeManagementService : IJavaRuntimeService
{
    Task<IReadOnlyList<JavaRuntimeDescriptor>> GetRuntimesAsync(
        CancellationToken cancellationToken = default);

    Task<JavaRuntimeManagementOutcome> ReverifyAsync(
        string executablePath,
        CancellationToken cancellationToken = default);

    Task<JavaRuntimeManagementOutcome> DeleteManagedAsync(
        string executablePath,
        CancellationToken cancellationToken = default);

    Task<string?> GetManualExecutablePathAsync(
        CancellationToken cancellationToken = default);

    Task<JavaRuntimeManagementOutcome> SaveManualExecutablePathAsync(
        string? executablePath,
        CancellationToken cancellationToken = default);
}
