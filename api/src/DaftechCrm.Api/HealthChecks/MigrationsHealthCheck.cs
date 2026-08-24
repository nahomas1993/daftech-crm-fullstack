using DaftechCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DaftechCrm.Api.HealthChecks;

/// <summary>
/// Reports whether the database schema actually matches the migrations
/// compiled into this build.
///
/// This exists because of a real outage: a migration was committed without
/// its Designer metadata, so EF never discovered it, <c>MigrateAsync</c>
/// reported success, and the new column was silently missing in production.
/// Nothing failed at startup — screens just began returning 500s. A pending
/// (or unrecognised) migration is now a visible, machine-checkable signal
/// instead of a mystery.
///
/// Tagged "ready": the process is alive, but this instance is serving a
/// schema it wasn't built for and shouldn't take traffic.
/// </summary>
public class MigrationsHealthCheck : IHealthCheck
{
    private readonly AppDbContext _db;

    public MigrationsHealthCheck(AppDbContext db) => _db = db;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            var pending = (await _db.Database.GetPendingMigrationsAsync(ct)).ToList();
            var applied = (await _db.Database.GetAppliedMigrationsAsync(ct)).ToList();
            var known = _db.Database.GetMigrations().ToList();

            // Migrations recorded in the database that this build knows nothing
            // about — i.e. the deployed code is older than the schema.
            var unknown = applied.Except(known).ToList();

            var data = new Dictionary<string, object>
            {
                ["appliedCount"] = applied.Count,
                ["knownCount"] = known.Count,
                ["pending"] = pending,
                ["unknownToThisBuild"] = unknown,
            };

            if (pending.Count > 0)
                return HealthCheckResult.Unhealthy(
                    $"{pending.Count} migration(s) have not been applied to the database: {string.Join(", ", pending)}.",
                    data: data);

            if (unknown.Count > 0)
                return HealthCheckResult.Degraded(
                    $"The database has {unknown.Count} migration(s) this build does not contain: {string.Join(", ", unknown)}. The deployed code may be behind the schema.",
                    data: data);

            return HealthCheckResult.Healthy($"Schema is up to date ({applied.Count} migration(s) applied).", data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Could not read migration history.", ex);
        }
    }
}
