using System.Net;
using System.Text.Json;
using Npgsql;

namespace DaftechCrm.Api.Middleware;

/// <summary>
/// Catches any exception that escapes a controller action and turns it
/// into a consistent JSON error response instead of leaking a stack trace.
/// Controllers still catch specific InvalidOperationException /
/// ArgumentOutOfRangeException cases themselves for precise 400/404/409
/// responses (see e.g. TicketsController) — this is the last-resort net
/// for anything unhandled.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception processing {Method} {Path}", context.Request.Method, context.Request.Path);

            if (context.Response.HasStarted)
                throw;

            context.Response.ContentType = "application/json";

            if (TryGetUniqueConstraintName(ex, out var constraintName))
            {
                // A duplicate email/username/etc. is a client mistake (already
                // registered), not a server fault — 409 with a message the
                // frontend can show directly, instead of the generic 500.
                context.Response.StatusCode = (int)HttpStatusCode.Conflict;
                await context.Response.WriteAsync(JsonSerializer.Serialize(new
                {
                    error = DescribeConstraintViolation(constraintName),
                    traceId = context.TraceIdentifier,
                }));
                return;
            }

            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            var payload = new
            {
                error = "An unexpected error occurred. Please try again, and contact support if the problem persists.",
                traceId = context.TraceIdentifier,
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
        }
    }

    /// <summary>
    /// EF Core wraps the driver exception in a DbUpdateException; unwrap to
    /// the underlying PostgresException to read its SQLSTATE and constraint
    /// name. 23505 is Postgres's "unique_violation" code.
    /// </summary>
    private static bool TryGetUniqueConstraintName(Exception ex, out string constraintName)
    {
        constraintName = "";
        var current = ex;
        while (current is not null)
        {
            if (current is PostgresException { SqlState: "23505" } pgEx)
            {
                constraintName = pgEx.ConstraintName ?? "";
                return true;
            }
            current = current.InnerException;
        }
        return false;
    }

    /// <summary>Best-effort human-readable message per known unique index name; falls back to a generic "already exists" message for anything not explicitly listed.</summary>
    private static string DescribeConstraintViolation(string constraintName) => constraintName switch
    {
        "IX_employees_Email" => "An employee with this email address is already registered.",
        "IX_employees_Username" => "That username is already taken — please try again.",
        "IX_clients_Email" => "A client with this email address is already registered.",
        "IX_clients_IdNumber" => "A client with this ID number is already registered.",
        "IX_employees_AccountRefId" or "IX_clients_AccountRefId" => "That account reference ID is already in use — please try again.",
        _ => "A record with these details already exists.",
    };
}
