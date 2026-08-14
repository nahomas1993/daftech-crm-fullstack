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
// appsettings.Development.json defaults this to the Angular dev server;
// appsettings.Production.json must set it to the real deployed frontend origin(s).
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:4200" };

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
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
