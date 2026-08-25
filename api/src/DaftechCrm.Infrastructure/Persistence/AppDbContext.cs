using DaftechCrm.Application.Interfaces;
using DaftechCrm.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DaftechCrm.Infrastructure.Persistence;

public class AppDbContext : DbContext, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Client> ClientsSet => Set<Client>();
    public DbSet<SystemProduct> SystemProductsSet => Set<SystemProduct>();
    public DbSet<AgreementType> AgreementTypesSet => Set<AgreementType>();
    public DbSet<Agreement> AgreementsSet => Set<Agreement>();
    public DbSet<TrainingAssignment> TrainingAssignmentsSet => Set<TrainingAssignment>();
    public DbSet<TrainingRecord> TrainingRecordsSet => Set<TrainingRecord>();
    public DbSet<Ticket> TicketsSet => Set<Ticket>();
    public DbSet<TicketAuditEntry> TicketAuditEntriesSet => Set<TicketAuditEntry>();
    public DbSet<Employee> EmployeesSet => Set<Employee>();
    public DbSet<DeviceSession> DeviceSessionsSet => Set<DeviceSession>();
    public DbSet<LoginRecord> LoginRecordsSet => Set<LoginRecord>();
    public DbSet<TimeLog> TimeLogsSet => Set<TimeLog>();
    public DbSet<MaintenanceRecord> MaintenanceRecordsSet => Set<MaintenanceRecord>();
    public DbSet<AppNotification> NotificationsSet => Set<AppNotification>();
    public DbSet<SatisfactionSurvey> SatisfactionSurveysSet => Set<SatisfactionSurvey>();
    public DbSet<LoginSession> LoginSessionsSet => Set<LoginSession>();
    public DbSet<RefreshToken> RefreshTokensSet => Set<RefreshToken>();
    public DbSet<SystemSetting> SystemSettingsSet => Set<SystemSetting>();
    public DbSet<PasswordResetRequest> PasswordResetRequestsSet => Set<PasswordResetRequest>();
    public DbSet<LocationEntry> LocationEntriesSet => Set<LocationEntry>();
    public DbSet<FailureType> FailureTypesSet => Set<FailureType>();
    public DbSet<SupportType> SupportTypesSet => Set<SupportType>();
    public DbSet<StoredFile> StoredFilesSet => Set<StoredFile>();

    // IAppDbContext — exposed as IQueryable so Application services never depend on DbSet<T> directly.
    public IQueryable<Client> Clients => ClientsSet;
    public IQueryable<SystemProduct> SystemProducts => SystemProductsSet;
    public IQueryable<AgreementType> AgreementTypes => AgreementTypesSet;
    public IQueryable<Agreement> Agreements => AgreementsSet;
    public IQueryable<TrainingAssignment> TrainingAssignments => TrainingAssignmentsSet;
    public IQueryable<TrainingRecord> TrainingRecords => TrainingRecordsSet;
    public IQueryable<Ticket> Tickets => TicketsSet;
    public IQueryable<TicketAuditEntry> TicketAuditEntries => TicketAuditEntriesSet;
    public IQueryable<Employee> Employees => EmployeesSet;
    public IQueryable<DeviceSession> DeviceSessions => DeviceSessionsSet;
    public IQueryable<LoginRecord> LoginRecords => LoginRecordsSet;
    public IQueryable<TimeLog> TimeLogs => TimeLogsSet;
    public IQueryable<MaintenanceRecord> MaintenanceRecords => MaintenanceRecordsSet;
    public IQueryable<AppNotification> Notifications => NotificationsSet;
    public IQueryable<SatisfactionSurvey> SatisfactionSurveys => SatisfactionSurveysSet;
    public IQueryable<LoginSession> LoginSessions => LoginSessionsSet;
    public IQueryable<RefreshToken> RefreshTokens => RefreshTokensSet;
    public IQueryable<SystemSetting> SystemSettings => SystemSettingsSet;
    public IQueryable<PasswordResetRequest> PasswordResetRequests => PasswordResetRequestsSet;
    public IQueryable<LocationEntry> LocationEntries => LocationEntriesSet;
    public IQueryable<FailureType> FailureTypes => FailureTypesSet;
    public IQueryable<SupportType> SupportTypes => SupportTypesSet;
    public IQueryable<StoredFile> StoredFiles => StoredFilesSet;

    public new void Add<TEntity>(TEntity entity) where TEntity : class => Set<TEntity>().Add(entity);
    public new void Update<TEntity>(TEntity entity) where TEntity : class => Set<TEntity>().Update(entity);
    public new void Remove<TEntity>(TEntity entity) where TEntity : class => Set<TEntity>().Remove(entity);
    public void Detach<TEntity>(TEntity entity) where TEntity : class => Entry(entity).State = EntityState.Detached;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Every entity in this model assigns its own primary key in the
        // property initializer (`public Guid Id { get; set; } = Guid.NewGuid();`)
        // — the database never generates one. EF Core, however, defaults Guid
        // keys to ValueGenerated.OnAdd, and it uses exactly that flag to decide
        // the state of an untracked entity it discovers through a navigation
        // property: a store-generated key that already holds a non-default
        // value means "this row exists" -> EntityState.Modified.
        //
        // That is what broke every ticket status change. `ticket.AuditTrail.Add(
        // new TicketAuditEntry { ... })` produced
        //     UPDATE ticket_audit_entries SET ... WHERE "Id" = @p
        // instead of an INSERT. The row did not exist, the batch reported 0
        // rows affected, EF raised DbUpdateConcurrencyException, and
        // TicketService turned that into HTTP 409 — with no concurrency token
        // anywhere in the model.
        //
        // Marking client-assigned Guid keys as ValueGeneratedNever tells EF the
        // truth, so a populated Id no longer implies an existing row and the
        // audit entry is correctly INSERTed. No DDL change: these columns never
        // had a database default.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var key = entityType.FindPrimaryKey();
            if (key is null) continue;

            foreach (var property in key.Properties)
            {
                if (property.ClrType == typeof(Guid))
                    property.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never;
            }
        }

        base.OnModelCreating(modelBuilder);
    }
}
