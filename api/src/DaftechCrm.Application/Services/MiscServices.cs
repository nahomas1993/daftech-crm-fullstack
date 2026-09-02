using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Domain.Entities;
using DaftechCrm.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DaftechCrm.Application.Services;

public class NotificationService : INotificationService
{
    private readonly IAppDbContext _db;
    public NotificationService(IAppDbContext db) => _db = db;

    public async Task NotifyAsync(NotificationRecipientType recipientType, string recipientId, string eventType, string message, CancellationToken ct = default)
    {
        _db.Add(new AppNotification
        {
            RecipientType = recipientType,
            RecipientId = recipientId,
            EventType = eventType,
            Message = message,
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<NotificationDto>> GetForRecipientAsync(NotificationRecipientType recipientType, string recipientId, CancellationToken ct = default) =>
        await _db.Notifications
            .Where(n => n.RecipientType == recipientType && n.RecipientId == recipientId)
            .OrderByDescending(n => n.DateSent)
            .Select(n => new NotificationDto(n.Id, n.RecipientType, n.RecipientId, n.EventType, n.Message, n.DateSent, n.ReadStatus))
            .ToListAsync(ct);

    public async Task<bool> MarkReadAsync(Guid notificationId, SessionAccountType callerType, Guid callerId, bool callerIsAdmin, CancellationToken ct = default)
    {
        var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == notificationId, ct);
        if (n is null) return false;

        var ownsRecipient =
            (n.RecipientType == NotificationRecipientType.Admin && callerIsAdmin) ||
            (n.RecipientType == NotificationRecipientType.Employee && callerType == SessionAccountType.Employee && n.RecipientId == callerId.ToString()) ||
            (n.RecipientType == NotificationRecipientType.Client && callerType == SessionAccountType.Client && n.RecipientId == callerId.ToString());

        if (!ownsRecipient) return false;

        n.ReadStatus = true;
        _db.Update(n);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task MarkAllReadAsync(NotificationRecipientType recipientType, string recipientId, CancellationToken ct = default)
    {
        var items = await _db.Notifications.Where(n => n.RecipientType == recipientType && n.RecipientId == recipientId && !n.ReadStatus).ToListAsync(ct);
        foreach (var n in items) { n.ReadStatus = true; _db.Update(n); }
        if (items.Count > 0) await _db.SaveChangesAsync(ct);
    }
}

public class MaintenanceService : IMaintenanceService
{
    private readonly IAppDbContext _db;
    public MaintenanceService(IAppDbContext db) => _db = db;

    public async Task<MaintenanceRecordDto> CreateAsync(CreateMaintenanceRecordRequest request, CancellationToken ct = default)
    {
        if (request.ClientId is null)
            throw new InvalidOperationException("Client is required.");

        var clientExists = await _db.Clients.AnyAsync(c => c.Id == request.ClientId, ct);
        if (!clientExists)
            throw new InvalidOperationException("Client not found.");

        if (request.SystemProductId is Guid systemProductId)
        {
            var product = await _db.SystemProducts.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == systemProductId && !s.IsDeleted, ct)
                ?? throw new InvalidOperationException("Selected system/product was not found.");
            if (product.ClientId != request.ClientId)
                throw new InvalidOperationException("Selected system/product does not belong to this client.");
        }

        if (request.TicketId is Guid ticketId)
        {
            var ticket = await _db.Tickets.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == ticketId, ct)
                ?? throw new InvalidOperationException("Selected ticket was not found.");
            if (ticket.ClientId != request.ClientId)
                throw new InvalidOperationException("Selected ticket does not belong to this client.");
        }

        var employeeExists = await _db.Employees.AnyAsync(e => e.Id == request.PerformedByEmployeeId, ct);
        if (!employeeExists)
            throw new InvalidOperationException("Performing employee not found.");

        var record = new MaintenanceRecord
        {
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            Category = request.Category,
            Description = request.Description,
            PerformedByEmployeeId = request.PerformedByEmployeeId,
            Status = request.Status,
            Remarks = request.Remarks,
            ClientId = request.ClientId,
            SystemProductId = request.SystemProductId,
            TicketId = request.TicketId,
        };
        _db.Add(record);
        await _db.SaveChangesAsync(ct);

        var loaded = await Query().FirstAsync(r => r.Id == record.Id, ct);
        return ToDto(loaded);
    }

    public async Task<IReadOnlyList<MaintenanceRecordDto>> GetAllAsync(CancellationToken ct = default) =>
        (await Query().OrderByDescending(r => r.Date).ToListAsync(ct)).Select(ToDto).ToList();

    public async Task<PagedResult<MaintenanceRecordDto>> GetAllPagedAsync(PaginationQuery query, CancellationToken ct = default)
    {
        var totalCount = await _db.MaintenanceRecords.CountAsync(ct);

        var items = await Query()
            .OrderByDescending(r => r.Date)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PagedResult<MaintenanceRecordDto>(items.Select(ToDto).ToList(), query.Page, query.PageSize, totalCount);
    }

    public async Task<IReadOnlyList<MaintenanceRecordDto>> GetForClientAsync(Guid clientId, CancellationToken ct = default) =>
        (await Query().Where(r => r.ClientId == clientId).OrderByDescending(r => r.Date).ToListAsync(ct)).Select(ToDto).ToList();

    public async Task<IReadOnlyList<MaintenanceRecordDto>> GetForSystemProductAsync(Guid systemProductId, CancellationToken ct = default) =>
        (await Query().Where(r => r.SystemProductId == systemProductId).OrderByDescending(r => r.Date).ToListAsync(ct)).Select(ToDto).ToList();

    private IQueryable<MaintenanceRecord> Query() =>
        _db.MaintenanceRecords.AsNoTracking()
            .Include(r => r.PerformedByEmployee)
            .Include(r => r.Client)
            .Include(r => r.SystemProduct);

    private static MaintenanceRecordDto ToDto(MaintenanceRecord r) => new(
        r.Id, r.Date, r.Category, r.Description,
        r.PerformedByEmployeeId, r.PerformedByEmployee?.FullName ?? "(unknown)", r.Status, r.Remarks,
        r.ClientId, r.Client?.Name, r.SystemProductId, r.SystemProduct?.Name, r.TicketId
    );
}

public class TimeLogService : ITimeLogService
{
    private readonly IAppDbContext _db;
    public TimeLogService(IAppDbContext db) => _db = db;

    public async Task ClockInAsync(Guid employeeId, CancellationToken ct = default)
    {
        _db.Add(new TimeLog
        {
            EmployeeId = employeeId,
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            StartTime = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task ClockOutAsync(Guid employeeId, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var log = await _db.TimeLogs.FirstOrDefaultAsync(l => l.EmployeeId == employeeId && l.Date == today && l.FinishTime == null, ct);
        if (log is null) return;

        log.FinishTime = DateTimeOffset.UtcNow;
        log.TotalHours = Math.Round((log.FinishTime.Value - log.StartTime!.Value).TotalHours, 2);
        _db.Update(log);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<TimeLogDto>> GetAllAsync(Guid? employeeId = null, CancellationToken ct = default)
    {
        var query = _db.TimeLogs.AsQueryable();
        if (employeeId is not null) query = query.Where(l => l.EmployeeId == employeeId);
        return await query.OrderByDescending(l => l.Date)
            .Select(l => new TimeLogDto(l.Id, l.EmployeeId, l.Date, l.StartTime, l.FinishTime, l.TotalHours))
            .ToListAsync(ct);
    }

    public async Task<PagedResult<TimeLogDto>> GetAllPagedAsync(Guid? employeeId, PaginationQuery query, CancellationToken ct = default)
    {
        var baseQuery = _db.TimeLogs.AsQueryable();
        if (employeeId is not null) baseQuery = baseQuery.Where(l => l.EmployeeId == employeeId);

        var totalCount = await baseQuery.CountAsync(ct);

        var items = await baseQuery
            .OrderByDescending(l => l.Date)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .Select(l => new TimeLogDto(l.Id, l.EmployeeId, l.Date, l.StartTime, l.FinishTime, l.TotalHours))
            .ToListAsync(ct);

        return new PagedResult<TimeLogDto>(items, query.Page, query.PageSize, totalCount);
    }
}
