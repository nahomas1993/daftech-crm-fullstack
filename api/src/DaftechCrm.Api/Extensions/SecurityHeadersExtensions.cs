namespace DaftechCrm.Api.Extensions;

/// <summary>
/// Adds standard defensive HTTP response headers. HSTS itself is
/// registered separately via app.UseHsts() (ASP.NET Core's built-in
/// middleware already implements it correctly) — this only adds the
/// headers that .NET has no built-in middleware for.
/// </summary>
public static class SecurityHeadersExtensions
{
    public static IApplicationBuilder UseDaftechSecurityHeaders(this IApplicationBuilder app, bool isProduction)
    {
        return app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;

            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            // X-XSS-Protection is deprecated in modern browsers (superseded by CSP) but
            // still requested — harmless to send, some older clients/scanners still check it.
            headers["X-XSS-Protection"] = "0";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";

            if (isProduction)
            {
                // 'self' only — this is a JSON API with a Swagger UI disabled in
                // production, so there's no inline script/style to allow for.
                headers["Content-Security-Policy"] =
                    "default-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
            }

            await next();
        });
    }
}
