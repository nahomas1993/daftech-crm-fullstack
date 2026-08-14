using DaftechCrm.Application.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DaftechCrm.Api.HealthChecks;

/// <summary>
/// Confirms the configured file storage backend is actually writable and
/// readable (round-trips a tiny probe file). Tagged "ready" — if uploads
/// are broken, this instance shouldn't take traffic that depends on them,
/// but the process itself is fine.
/// </summary>
public class StorageHealthCheck : IHealthCheck
{
    private readonly IFileStorageService _storage;

    public StorageHealthCheck(IFileStorageService storage) => _storage = storage;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            var ok = await _storage.ProbeAsync(ct);
            return ok
                ? HealthCheckResult.Healthy("File storage is writable and readable.")
                : HealthCheckResult.Unhealthy("File storage probe failed.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("File storage check failed.", ex);
        }
    }
}
