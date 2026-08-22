using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace DaftechCrm.Api.Extensions;

/// <summary>
/// Rate limiting policies built on .NET 8's built-in
/// System.Threading.RateLimiting — no third-party package needed.
/// "Global" applies to every request by client IP; "AuthEndpoints" is a
/// stricter policy applied only to login/refresh routes to slow down
/// credential-stuffing and brute-force attempts.
/// </summary>
public static class RateLimitingExtensions
{
    public const string AuthPolicy = "AuthEndpoints";

    public static IServiceCollection AddDaftechRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.Headers["Retry-After"] = "60";
                context.HttpContext.Response.ContentType = "application/json";
                await context.HttpContext.Response.WriteAsync(
                    """{"error":"Too many requests. Please try again shortly."}""", ct);
            };

            // Applied to every request automatically via app.UseRateLimiter() —
            // no per-endpoint attribute needed for the global 100/min limit.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: ResolvePartitionKey(httpContext),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 100,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));

            // 10 attempts/minute per client IP — applied explicitly to
            // login/refresh endpoints via [EnableRateLimiting(AuthPolicy)],
            // stacking on top of (and stricter than) the global limiter above.
            options.AddPolicy(AuthPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: ResolvePartitionKey(httpContext),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));
        });

        return services;
    }

    private static string ResolvePartitionKey(HttpContext httpContext) =>
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
