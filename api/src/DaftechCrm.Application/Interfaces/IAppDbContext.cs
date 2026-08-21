using DaftechCrm.Domain.Entities;

namespace DaftechCrm.Application.Interfaces;

/// <summary>
/// Minimal repository/unit-of-work seam so Application services depend on
/// abstractions, not EF Core directly (Clean Architecture / DDD, matching
/// the pattern used on the Trade License Workflow project).
/// </summary>
public interface IAppDbContext
{
    IQueryable<Client> Clients { get; }
    IQueryable<SystemProduct> SystemProducts { get; }
    IQueryable<AgreementType> AgreementTypes { get; }
    IQueryable<Agreement> Agreements { get; }
    IQueryable<TrainingSession> TrainingSessions { get; }
    IQueryable<Ticket> Tickets { get; }
    IQueryable<TicketAuditEntry> TicketAuditEntries { get; }
    IQueryable<Employee> Employees { get; }
    IQueryable<DeviceSession> DeviceSessions { get; }
    IQueryable<LoginRecord> LoginRecords { get; }
    IQueryable<TimeLog> TimeLogs { get; }
    IQueryable<MaintenanceRecord> MaintenanceRecords { get; }
    IQueryable<AppNotification> Notifications { get; }
    IQueryable<SatisfactionSurvey> SatisfactionSurveys { get; }
    IQueryable<LoginSession> LoginSessions { get; }
    IQueryable<RefreshToken> RefreshTokens { get; }
    IQueryable<SystemSetting> SystemSettings { get; }
    IQueryable<PasswordResetRequest> PasswordResetRequests { get; }
    IQueryable<LocationEntry> LocationEntries { get; }
    IQueryable<FailureType> FailureTypes { get; }
    IQueryable<StoredFile> StoredFiles { get; }

    void Add<TEntity>(TEntity entity) where TEntity : class;
    void Update<TEntity>(TEntity entity) where TEntity : class;
    void Remove<TEntity>(TEntity entity) where TEntity : class;

    /// <summary>
    /// Stops tracking the given entity (if it's currently tracked) without
    /// deleting it — used to recover from a failed SaveChangesAsync that
    /// left a stale/poisoned instance in the context's identity map, so a
    /// retry's fresh query actually hits the database instead of getting
    /// the same bad tracked instance back.
    /// </summary>
    void Detach<TEntity>(TEntity entity) where TEntity : class;

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
