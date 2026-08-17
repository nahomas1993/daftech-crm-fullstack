using System.Text;
using DaftechCrm.Api.Auth;
using DaftechCrm.Api.BackgroundServices;
using DaftechCrm.Api.Extensions;
using DaftechCrm.Api.HealthChecks;
using DaftechCrm.Api.Middleware;
using DaftechCrm.Api.Services;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Application.Options;
using DaftechCrm.Domain.Enums;
using DaftechCrm.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

const string CorsPolicy = "DaftechCors";

// Enums travel as string names (e.g. "Laptop", not 2) in every request/response
// across the API — the Angular frontend sends and expects strings throughout.
// Without this, System.Text.Json only accepts/emits the numeric value, and
// every request carrying an enum field (DeviceType, TicketCategory, etc.)
// fails model binding with a 400 before it reaches a controller.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSwaggerGen(options =>
    {
        options.AddSecurityDefinition("Bearer", new()
        {
            Name = "Authorization",
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Description = "Paste the access token only — no 'Bearer ' prefix needed here.",
        });
        options.AddSecurityRequirement(new()
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" }
                },
                Array.Empty<string>()
            }
        });
    });
}

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentRequestContext, HttpCurrentRequestContext>();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddDaftechRateLimiting();

// "live" = process is up, no dependency checks — used by orchestrators to
// decide whether to restart the container.
// "ready" = can this instance actually serve traffic — checked against
// hard dependencies (DB, storage). Email is intentionally NOT tagged
// "ready" (see EmailHealthCheck/BrevoApiHealthCheck) since it's a soft
// dependency.
var emailProvider = builder.Configuration.GetSection(EmailOptions.SectionName).Get<EmailOptions>()?.Provider ?? EmailProvider.Smtp;
var healthChecksBuilder = builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"])
    .AddCheck<StorageHealthCheck>("storage", tags: ["ready"]);

if (emailProvider == EmailProvider.BrevoApi)
    healthChecksBuilder.AddCheck<BrevoApiHealthCheck>("email", tags: ["email"]);
else
    healthChecksBuilder.AddCheck<EmailHealthCheck>("email", tags: ["email"]);

builder.Services.AddHostedService<AutoCloseTicketsHostedService>();
builder.Services.AddHostedService<SessionSweepHostedService>();

// ---- Authentication / Authorization ----
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey) || Encoding.UTF8.GetByteCount(jwtOptions.SigningKey) < 32)
{
    if (builder.Environment.IsDevelopment())
    {
        throw new InvalidOperationException(
            "Jwt:SigningKey is missing or shorter than 32 bytes. Set it with: " +
            "dotnet user-secrets set \"Jwt:SigningKey\" \"$(openssl rand -base64 48)\" " +
            "from src/DaftechCrm.Api");
    }
    throw new InvalidOperationException(
        "Jwt:SigningKey is missing or shorter than 32 bytes. Set the Jwt__SigningKey environment " +
        "variable in production (generate with: openssl rand -base64 48).");
}

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ValidateLifetime = true,
            // Access tokens are short-lived by design (see JwtOptions.AccessTokenMinutes) —
            // no extra clock-skew allowance needed beyond the library default.
            ClockSkew = TimeSpan.FromSeconds(30),
        };

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogDebug(context.Exception, "JWT authentication failed.");
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization(options => options.AddDaftechPolicies());

// ---- CORS ----
// Allowed origins come from configuration (Cors:AllowedOrigins), not a
// hardcoded localhost value, so the same build works in every environment.
// The base appsettings.json defaults this to the Angular dev server
// (http://localhost:4200); appsettings.Production.json's empty array does
// NOT itself override that default (ASP.NET Core's config layering can't
// clear an array with an empty one) — Cors__AllowedOrigins__0 must be set
// as a real environment variable on the Render service. The check below
// makes it loudly obvious in the logs if that env var was forgotten,
// instead of the app silently starting up with CORS pointed at localhost.
var devDefaultOrigin = "http://localhost:4200";
var configuredOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? Array.Empty<string>();

// Production fallback: the deployed frontend calls this API cross-origin
// (absolute apiBaseUrl in frontend/src/environments/environment.production.ts),
// so falling through to the localhost:4200 dev default would block every real
// login with a CORS error that looks, in the browser, exactly like "could not
// reach the server". A forgotten Cors__AllowedOrigins__0 env var must not be
// able to take production down: fall back to the known deployed frontend
// origins instead of localhost. Setting the env var still overrides this
// (e.g. for a custom domain like https://crm.daftech.et).
var productionFallbackOrigins = new[]
{
    "https://daftech-crm-frontend.onrender.com",
    "https://daftech-crm.onrender.com"
};

var effectiveConfigured = configuredOrigins
    .Where(o => !string.IsNullOrWhiteSpace(o))
    .Select(o => o.TrimEnd('/'))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();

var isDevDefaultOnly = effectiveConfigured.Length == 0 ||
    (effectiveConfigured.Length == 1 &&
     string.Equals(effectiveConfigured[0], devDefaultOrigin, StringComparison.OrdinalIgnoreCase));

string[] allowedOrigins;
if (builder.Environment.IsProduction() && isDevDefaultOnly)
{
    allowedOrigins = productionFallbackOrigins;
    Console.Error.WriteLine(
        "WARNING: Cors__AllowedOrigins__0 is not set on this service; falling back to the " +
        "known deployed frontend origins (" + string.Join(", ", productionFallbackOrigins) + "). " +
        "Set Cors__AllowedOrigins__0 explicitly to the real frontend origin (including any " +
        "custom domain) so browser requests are not blocked by CORS.");
}
else
{
    allowedOrigins = effectiveConfigured.Length > 0 ? effectiveConfigured : new[] { devDefaultOrigin };
}

var isProductionEnvironment = builder.Environment.IsProduction();

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
    {
        // SetIsOriginAllowed rather than a plain WithOrigins list: besides the
        // explicitly configured origins, any *.onrender.com origin is accepted
        // in Production. The frontend is Render-hosted and its subdomain has
        // changed across redeploys/recreations, and a stale or mistyped
        // Cors__AllowedOrigins__0 makes the browser drop the login response
        // with no CORS header — which the UI can only report as "Could not
        // reach the server", exactly the reported bug. No credentials/cookies
        // are used (auth is a Bearer JWT), so this does not widen any
        // cookie-based attack surface.
        policy.SetIsOriginAllowed(origin =>
              {
                  if (string.IsNullOrWhiteSpace(origin)) return false;

                  var normalized = origin.TrimEnd('/');
                  if (allowedOrigins.Any(o =>
                          string.Equals(o.TrimEnd('/'), normalized, StringComparison.OrdinalIgnoreCase)))
                  {
                      return true;
                  }

                  if (!isProductionEnvironment) return false;

                  return Uri.TryCreate(normalized, UriKind.Absolute, out var uri)
                      && uri.Scheme == Uri.UriSchemeHttps
                      && (uri.Host.EndsWith(".onrender.com", StringComparison.OrdinalIgnoreCase)
                          || uri.Host.EndsWith(".daftech.et", StringComparison.OrdinalIgnoreCase));
              })
              .AllowAnyHeader()
              .AllowAnyMethod()
              // Response headers are NOT covered by AllowAnyHeader (that
              // only governs which request headers the browser may send).
              // Without this, browsers only expose the small CORS-safelisted
              // header set to JS — CallerIdentity.OwnershipForbiddenHeader
              // would be silently invisible to the frontend's auth
              // interceptor in any cross-origin scenario (e.g. local dev,
              // ng serve calling the API directly rather than through the
              // same-origin nginx proxy used in production).
              .WithExposedHeaders(CallerIdentity.OwnershipForbiddenHeader);
    });
});

if (!builder.Environment.IsDevelopment())
{
    builder.Services.AddHsts(options =>
    {
        options.MaxAge = TimeSpan.FromDays(365);
        options.IncludeSubDomains = true;
        options.Preload = true;
    });
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
}

app.UseDaftechSecurityHeaders(isProduction: app.Environment.IsProduction());
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();
app.UseCors(CorsPolicy);
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// /health/live: no dependency checks by design — confirms only that the process
// is up and responding, for orchestrators deciding whether to restart the container.
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = HealthCheckResponseWriter.WriteAsync,
});

// /health/ready: hard dependencies only (database, storage) — used by a load
// balancer/orchestrator to decide whether to route traffic to this instance.
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = HealthCheckResponseWriter.WriteAsync,
});

// /health: everything, including soft dependencies like email (reported as
// Degraded rather than failing the response) — useful for a dashboard/on-call view.
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = HealthCheckResponseWriter.WriteAsync,
});

// Apply pending EF Core migrations and seed baseline data on startup.
await app.Services.MigrateAndSeedAsync();

app.Run();

// Exposed for WebApplicationFactory-based integration tests (see DaftechCrm.Tests).
public partial class Program { }
