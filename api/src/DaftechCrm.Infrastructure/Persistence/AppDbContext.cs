using DaftechCrm.Application.Interfaces;
using DaftechCrm.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DaftechCrm.Infrastructure.Persistence;

public class AppDbContext : DbContext, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Client> ClientsSet => Set<Client>();
    public DbSet<Agreement> AgreementsSet => Set<Agreement>();
    public DbSet<AgreementTraining> AgreementTrainingsSet => Set<AgreementTraining>();
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
    public DbSet<StoredFile> StoredFilesSet => Set<StoredFile>();

    // IAppDbContext — exposed as IQueryable so Application services never depend on DbSet<T> directly.
    public IQueryable<Client> Clients => ClientsSet;
    public IQueryable<Agreement> Agreements => AgreementsSet;
    public IQueryable<AgreementTraining> AgreementTrainings => AgreementTrainingsSet;
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
    public IQueryable<StoredFile> StoredFiles => StoredFilesSet;

    public void Add<TEntity>(TEntity entity) where TEntity : class => Set<TEntity>().Add(entity);
    public void Update<TEntity>(TEntity entity) where TEntity : class => Set<TEntity>().Update(entity);
    public void Remove<TEntity>(TEntity entity) where TEntity : class => Set<TEntity>().Remove(entity);
    public void Detach<TEntity>(TEntity entity) where TEntity : class => Entry(entity).State = EntityState.Detached;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
