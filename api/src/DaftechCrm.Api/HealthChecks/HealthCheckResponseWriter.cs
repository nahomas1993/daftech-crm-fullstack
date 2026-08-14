using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DaftechCrm.Api.HealthChecks;

/// <summary>
/// Writes health check results as JSON with per-check status and duration,
/// instead of the default plain-text "Healthy"/"Unhealthy" body — useful
/// for dashboards and for debugging which specific dependency is down.
/// </summary>
public static class HealthCheckResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                durationMs = e.Value.Duration.TotalMilliseconds,
            }),
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions));
    }
}
