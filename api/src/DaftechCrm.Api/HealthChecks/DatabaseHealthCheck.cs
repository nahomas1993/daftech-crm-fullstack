using DaftechCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DaftechCrm.Api.HealthChecks;

/// <summary>
/// Confirms the API can actually reach and query PostgreSQL — not just that the
/// connection string parses. Tagged "ready" (not "live"): a database
/// outage means this instance shouldn't receive traffic, but the process
/// itself is still fine and shouldn't be restarted for it.
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly AppDbContext _db;

    public DatabaseHealthCheck(AppDbContext db) => _db = db;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            // CanConnectAsync alone can succeed against a server that's up but the
            // target schema/table is missing — running a trivial real query against
            // a mapped table catches that case too.
            var canConnect = await _db.Database.CanConnectAsync(ct);
            if (!canConnect)
                return HealthCheckResult.Unhealthy("Cannot connect to the PostgreSQL database.");

            _ = await _db.Employees.Select(e => e.Id).FirstOrDefaultAsync(ct);
            return HealthCheckResult.Healthy("Database is reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database check failed.", ex);
        }
    }
}
