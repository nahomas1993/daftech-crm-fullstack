using DaftechCrm.Application.Interfaces;
using DaftechCrm.Application.Options;
using DaftechCrm.Application.Services;
using DaftechCrm.Domain.Enums;
using DaftechCrm.Infrastructure.Ai;
using DaftechCrm.Infrastructure.Auth;
using DaftechCrm.Infrastructure.Email;
using DaftechCrm.Infrastructure.Persistence;
using DaftechCrm.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DaftechCrm.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Read DATABASE_URL directly from environment variable
        var connectionString = Environment.GetEnvironmentVariable("DATABASE_URL");

        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("DATABASE_URL environment variable is missing or empty.");

        // Parse the connection string if it is in URL format
        if (connectionString.StartsWith("postgres://") ||
            connectionString.StartsWith("postgresql://"))
        {
            try
            {
                var uri = new Uri(connectionString);

                var userInfo = uri.UserInfo.Split(':', 2);

                if (userInfo.Length == 0 || string.IsNullOrWhiteSpace(userInfo[0]))
                {
                    throw new InvalidOperationException("DATABASE_URL is missing username.");
                }

                var builder = new Npgsql.NpgsqlConnectionStringBuilder
                {
                    Host = uri.Host,
                    Port = uri.IsDefaultPort ? 5432 : uri.Port,
                    Database = uri.AbsolutePath.Trim('/'),
                    Username = userInfo[0],
                    Password = userInfo.Length > 1 ? userInfo[1] : "",
                    SslMode = Npgsql.SslMode.Require
                };

                // Preserve additional query parameters
                var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);

                if (!string.IsNullOrEmpty(queryParams["sslmode"]))
                {
                    builder.SslMode = Enum.Parse<Npgsql.SslMode>(
                        queryParams["sslmode"]!,
                        true);
                }

                if (!string.IsNullOrEmpty(queryParams["connect_timeout"]))
                {
                    builder.Timeout = int.Parse(queryParams["connect_timeout"]!);
                }

                if (!string.IsNullOrEmpty(queryParams["pooling"]))
                {
                    builder.Pooling = bool.Parse(queryParams["pooling"]!);
                }

                connectionString = builder.ConnectionString;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to parse DATABASE_URL: {ex.Message}. " +
                    "Ensure it is in the format: postgres://user:password@host:port/database",
                    ex);
            }
        }

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IAppDbContext>(sp =>
            sp.GetRequiredService<AppDbContext>());

        services.Configure<TicketWorkflowOptions>(
            configuration.GetSection(TicketWorkflowOptions.SectionName));

        services.Configure<SessionOptions>(
            configuration.GetSection(SessionOptions.SectionName));

        services.Configure<SmtpOptions>(
            configuration.GetSection(SmtpOptions.SectionName));

        services.Configure<BrevoApiOptions>(
            configuration.GetSection(BrevoApiOptions.SectionName));

        var emailOptions = configuration.GetSection(EmailOptions.SectionName).Get<EmailOptions>() ?? new EmailOptions();
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));

        if (emailOptions.Provider == EmailProvider.BrevoApi)
            services.AddHttpClient<IEmailSender, BrevoApiEmailSender>();
        else
            services.AddScoped<IEmailSender, MailKitEmailSender>();

        services.Configure<JwtOptions>(
            configuration.GetSection(JwtOptions.SectionName));

        services.AddScoped<ITokenService, TokenService>();

        services.Configure<StorageOptions>(
            configuration.GetSection(StorageOptions.SectionName));

        services.Configure<CloudinaryOptions>(
            configuration.GetSection(CloudinaryOptions.SectionName));

        var storageOptions = configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>() ?? new StorageOptions();

        if (storageOptions.Provider == StorageProvider.Cloudinary)
            services.AddHttpClient<IFileStorageService, CloudinaryFileStorageService>();
        else if (storageOptions.Provider == StorageProvider.Postgres)
            services.AddSingleton<IFileStorageService, PostgresFileStorageService>();
        else
            services.AddSingleton<IFileStorageService, LocalFileStorageService>();

        services.AddScoped<AccountCredentialService>();
        services.AddScoped<ReferenceNumberService>();
        services.AddScoped<AccountReferenceIdService>();
        services.AddScoped<ITicketAssignmentService, TicketAssignmentService>();
        services.AddScoped<ITicketService, TicketService>();
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPasswordResetService, PasswordResetService>();
        services.AddScoped<IClientService, ClientService>();
        services.AddScoped<ISystemProductService, SystemProductService>();
        services.AddScoped<ITrainingRecordService, TrainingRecordService>();
        services.AddScoped<IAgreementTypeService, AgreementTypeService>();
        services.AddScoped<IAgreementService, AgreementService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IMaintenanceService, MaintenanceService>();
        services.AddScoped<ITimeLogService, TimeLogService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<ITicketReportService, TicketReportService>();
        services.AddSingleton<IEthiopianTimeService, EthiopianTimeService>();
        services.AddScoped<ITrainerWorkloadService, TrainerWorkloadService>();
        services.AddScoped<ISessionService, SessionService>();

        services.Configure<AiReportingOptions>(
            configuration.GetSection(AiReportingOptions.SectionName));

        services.AddHttpClient<IAiNarrativeReportService, AnthropicNarrativeReportService>();

        services.AddScoped<ISatisfactionSurveyService, SatisfactionSurveyService>();

        services.AddScoped<ISystemConfigurationService, SystemConfigurationService>();

        services.AddScoped<ILocationService, LocationService>();
        services.AddScoped<IFailureTypeService, FailureTypeService>();
        services.AddScoped<ISupportTypeService, SupportTypeService>();

        return services;
    }

    /// <summary>
    /// Applies pending migrations, inserts the full seed data set if the
    /// database is empty, and separately guarantees the fixed demo accounts
    /// exist on every startup (see EnsureDemoAccountsAsync below) —
    /// regardless of whatever else is already in the database. Call once
    /// at startup.
    /// </summary>
    public static async Task MigrateAndSeedAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger("DaftechCrm.Migrations");

        await MigrateAndVerifyAsync(db, logger);

        // AgreementTypes (Support/Training) must exist before anything else
        // seeds an Agreement against them, AND Support specifically must
        // exist on every startup (not just a fresh database) since the
        // training-before-support gate depends on it by name — see
        // EnsureCoreAgreementTypesAsync. Training is kept seeded too, as a
        // lookup value, even though no business rule keys off it anymore.
        await EnsureCoreAgreementTypesAsync(db);

        if (!await db.EmployeesSet.AnyAsync())
        {
            db.EmployeesSet.AddRange(SeedData.Employees());
            db.ClientsSet.AddRange(SeedData.Clients());
            db.SystemProductsSet.AddRange(SeedData.SystemProducts());
            await db.SaveChangesAsync();

            // Training roster/log depend only on SystemProducts existing
            // (TrainingAssignment/TrainingRecord key off SystemProductId,
            // not off any Agreement) — seeded independently of Agreements
            // below.
            db.TrainingAssignmentsSet.AddRange(SeedData.TrainingAssignments());
            db.TrainingRecordsSet.AddRange(SeedData.TrainingRecords());
            await db.SaveChangesAsync();

            db.AgreementsSet.AddRange(SeedData.Agreements());
            await db.SaveChangesAsync();
        }

        await EnsureDemoAccountsAsync(db);
    }


    /// <summary>
    /// Applies migrations and then *verifies* the result, instead of trusting
    /// that MigrateAsync silently did the right thing.
    ///
    /// Background: a migration was once committed without its Designer
    /// metadata file. EF therefore never discovered it, MigrateAsync
    /// completed "successfully", the new column was never created, and every
    /// screen that read it started returning 500s in production with no
    /// startup error to point at. This check turns that class of failure into
    /// a loud, immediate startup failure with an actionable message.
    /// </summary>
    private static async Task MigrateAndVerifyAsync(AppDbContext db, ILogger? logger)
    {
        var known = db.Database.GetMigrations().ToList();
        var pendingBefore = (await db.Database.GetPendingMigrationsAsync()).ToList();

        logger?.LogInformation(
            "Migrations: {KnownCount} compiled into this build, {PendingCount} pending before startup migration. Pending: {Pending}",
            known.Count, pendingBefore.Count, pendingBefore.Count == 0 ? "(none)" : string.Join(", ", pendingBefore));

        await db.Database.MigrateAsync();

        var pendingAfter = (await db.Database.GetPendingMigrationsAsync()).ToList();
        if (pendingAfter.Count > 0)
        {
            throw new InvalidOperationException(
                "Database migration did not complete: the following migrations are still pending after MigrateAsync: " +
                string.Join(", ", pendingAfter) +
                ". Refusing to start against a schema this build was not compiled for.");
        }

        var applied = (await db.Database.GetAppliedMigrationsAsync()).ToList();

        // Migration files that exist in the repository but were never compiled
        // into the migrations list are invisible to EF — exactly the failure
        // mode that caused the dashboard/report outage. Surface it loudly.
        var unknownToBuild = applied.Except(known).ToList();
        if (unknownToBuild.Count > 0)
        {
            logger?.LogWarning(
                "The database contains {Count} migration(s) this build does not know about ({Migrations}). The deployed code may be older than the schema.",
                unknownToBuild.Count, string.Join(", ", unknownToBuild));
        }

        logger?.LogInformation("Migrations verified: schema is up to date ({AppliedCount} applied).", applied.Count);
    }

    /// <summary>
    /// Guarantees the two business-rule-critical AgreementTypes (Support,
    /// Training — see AgreementTypeNames) exist, on every startup,
    /// regardless of whether the database is fresh or already has data.
    /// Upserts by Name rather than by the fixed seed Guid, since an older
    /// deploy predating this migration won't have the row at all yet.
    /// </summary>
    private static async Task EnsureCoreAgreementTypesAsync(AppDbContext db)
    {
        foreach (var coreType in SeedData.CoreAgreementTypes())
        {
            var exists = await db.AgreementTypesSet.AnyAsync(t => t.Name == coreType.Name);
            if (!exists)
                db.AgreementTypesSet.Add(coreType);
        }

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Guarantees the four fixed demo accounts (na1001/ns1002/at2001/mm2002,
    /// password "DaftechDemo1!") exist with a working password and a real
    /// AccountRefId, on every single startup — not just on a fresh/empty
    /// database like the seeding above. Upserts by Username: if the row is
    /// missing, it's inserted; if it already exists (e.g. from before this
    /// upsert step existed, or from an older deploy), its password hash and
    /// AccountRefId are corrected in place rather than left stale. This
    /// exists specifically so demo logins keep working across redeploys
    /// even against a database that already has older data in it — the
    /// original SeedData-based seeding above only ever runs once, the very
    /// first time the Employees table is empty.
    /// </summary>
    private static async Task EnsureDemoAccountsAsync(AppDbContext db)
    {
        foreach (var demo in SeedData.DemoEmployees())
        {
            var existing = await db.EmployeesSet.FirstOrDefaultAsync(e => e.Username == demo.Username);
            if (existing is null)
            {
                // Guard against the fixed seed Guid or Email colliding with
                // some unrelated existing row (e.g. a different account
                // that happens to reuse Emp1Admin's Id from an earlier,
                // differently-shaped deploy). Falls back to a fresh random
                // Id and a de-duplicated email rather than letting
                // SaveChangesAsync fail on a constraint violation.
                if (await db.EmployeesSet.AnyAsync(e => e.Id == demo.Id))
                    demo.Id = Guid.NewGuid();
                if (await db.EmployeesSet.AnyAsync(e => e.Email == demo.Email))
                    demo.Email = $"{demo.Username}@daftech.et";

                db.EmployeesSet.Add(demo);
            }
            else
            {
                // Guard the AccountRefId update too — if some other row
                // already holds this exact id (e.g. a leftover from a
                // previous partial run), skip overwriting to avoid a
                // unique-index violation; the existing row keeps whatever
                // AccountRefId it already has.
                var refIdTakenElsewhere = await db.EmployeesSet.AnyAsync(e => e.AccountRefId == demo.AccountRefId && e.Id != existing.Id);
                if (!refIdTakenElsewhere) existing.AccountRefId = demo.AccountRefId;
                existing.PasswordHash = demo.PasswordHash;
                existing.MustChangePassword = demo.MustChangePassword;
                existing.AccountStatus = demo.AccountStatus;
            }
        }

        foreach (var demo in SeedData.DemoClients())
        {
            var existing = await db.ClientsSet.FirstOrDefaultAsync(c => c.Username == demo.Username);
            if (existing is null)
            {
                if (await db.ClientsSet.AnyAsync(c => c.Id == demo.Id))
                    demo.Id = Guid.NewGuid();
                if (await db.ClientsSet.AnyAsync(c => c.Email == demo.Email))
                    demo.Email = $"{demo.Username}@daftech.et";
                if (await db.ClientsSet.AnyAsync(c => c.IdNumber == demo.IdNumber))
                    demo.IdNumber = $"ID-{demo.Username}";

                db.ClientsSet.Add(demo);
            }
            else
            {
                var refIdTakenElsewhere = await db.ClientsSet.AnyAsync(c => c.AccountRefId == demo.AccountRefId && c.Id != existing.Id);
                if (!refIdTakenElsewhere) existing.AccountRefId = demo.AccountRefId;
                existing.PasswordHash = demo.PasswordHash;
                existing.MustChangePassword = demo.MustChangePassword;
                existing.AccountStatus = demo.AccountStatus;
            }
        }

        await db.SaveChangesAsync();
    }
}