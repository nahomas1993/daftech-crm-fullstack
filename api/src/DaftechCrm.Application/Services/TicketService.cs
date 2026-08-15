using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Application.Options;
using DaftechCrm.Domain.Entities;
using DaftechCrm.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DaftechCrm.Application.Services;

public class TicketService : ITicketService
{
    private readonly IAppDbContext _db;
    private readonly ITicketAssignmentService _assignment;
    private readonly INotificationService _notifications;
    private readonly ISystemConfigurationService _config;
    private readonly IFileStorageService _storage;
    private readonly ILogger<TicketService> _logger;

    public TicketService(
        IAppDbContext db,
        ITicketAssignmentService assignment,
        INotificationService notifications,
        ISystemConfigurationService config,
        IFileStorageService storage,
        ILogger<TicketService> logger)
    {
        _db = db;
        _assignment = assignment;
        _notifications = notifications;
        _config = config;
        _storage = storage;
        _logger = logger;
    }

    public async Task<TicketDto> SubmitFromClientAsync(
        SubmitTicketRequest request,
        CancellationToken ct = default)
    {
        if (request.Description.Length > 1000)
            throw new ValidationException(
                "Description must be 1000 characters or fewer.");

        var agreement = await _db.Agreements
            .AsNoTracking()
            .FirstOrDefaultAsync(
                a => a.Id == request.AgreementId,
                ct)
            ?? throw new InvalidOperationException(
                "Agreement not found.");

        var chargeable =
            !agreement.IsWithinSupportWindow(
                DateOnly.FromDateTime(DateTime.UtcNow));

        var ticket = new Ticket
        {
            ClientId = request.ClientId,
            AgreementId = request.AgreementId,
            Description = request.Description,
            Category = request.Category,
            FailureTypeId = request.FailureTypeId,
            Chargeable = chargeable,
            Status = TicketStatus.Submitted,
            VoiceNoteStorageKey = request.VoiceNoteStorageKey,
            VoiceNoteFileName = request.VoiceNoteFileName,
        };

        ticket.AuditTrail.Add(
            new TicketAuditEntry
            {
                TicketId = ticket.Id,
                Actor = "Client",
                Action = "Submitted ticket"
            });

        if (!string.IsNullOrEmpty(request.VoiceNoteStorageKey))
        {
            ticket.AuditTrail.Add(
                new TicketAuditEntry
                {
                    TicketId = ticket.Id,
                    Actor = "Client",
                    Action = "Attached a voice-note recording with the issue description"
                });
        }

        var assignee =
            await _assignment.SelectAssigneeAsync(ct);

        if (assignee is not null)
        {
            ticket.AssignedEmployeeId = assignee.Id;
            ticket.AssignedAt = DateTimeOffset.UtcNow;
            ticket.Status = TicketStatus.Assigned;

            ticket.AuditTrail.Add(
                new TicketAuditEntry
                {
                    TicketId = ticket.Id,
                    Actor = "System",
                    Action =
                        $"Auto-assigned to {assignee.FullName} on submission (lowest open-ticket count)"
                });
        }

        _db.Add(ticket);

        await _db.SaveChangesAsync(ct);

        await _notifications.NotifyAsync(
            NotificationRecipientType.Admin,
            "ALL_ADMIN",
            "new_ticket",
            $"New ticket {ticket.Id} submitted.",
            ct);

        if (assignee is not null)
        {
            await _notifications.NotifyAsync(
                NotificationRecipientType.Employee,
                assignee.Id.ToString(),
                "ticket_assigned",
                $"You were assigned ticket {ticket.Id}.",
                ct);

            await _notifications.NotifyAsync(
                NotificationRecipientType.Client,
                ticket.ClientId.ToString(),
                "ticket_assigned",
                $"Your ticket {ticket.Id} has been assigned to a technician.",
                ct);
        }
        else
        {
            await _notifications.NotifyAsync(
                NotificationRecipientType.Admin,
                "ALL_ADMIN",
                "assignment_failed",
                $"Ticket {ticket.Id} submitted but no eligible employee was available for auto-assignment.",
                ct);
        }

        return await LoadDtoAsync(ticket.Id, ct);
    }

    public async Task<TicketDto> UpdateStatusAsync(
        Guid ticketId,
        UpdateTicketStatusRequest request,
        CancellationToken ct = default)
    {
        var ticket = await _db.Tickets
            .FirstOrDefaultAsync(t => t.Id == ticketId, ct)
            ?? throw new InvalidOperationException(
                "Ticket not found.");

        try
        {
            if (request.Status == TicketStatus.Resolved)
            {
                ticket.Status =
                    TicketStatus.AwaitingClientConfirmation;

                ticket.ResolvedAt =
                    DateTimeOffset.UtcNow;

                var confirmationWindowDays =
                    await _config.GetIntAsync(
                        "TicketWorkflow.ClientConfirmationWindowDays",
                        ct);

                ticket.ClientConfirmationDeadline =
                    ticket.ResolvedAt.Value.AddDays(
                        confirmationWindowDays);

                ticket.AuditTrail.Add(
                    new TicketAuditEntry
                    {
                        TicketId = ticket.Id,
                        Actor = request.ActorName,
                        Action =
                            $"Marked Resolved by {request.ActorName}; awaiting client confirmation (deadline {ticket.ClientConfirmationDeadline:u})"
                    });

                await _db.SaveChangesAsync(ct);

                // The status change is already committed above — a failure here
                // (e.g. notification write) must never surface as a failed status
                // update, since from the caller's point of view the update already
                // succeeded.
                try
                {
                    await _notifications.NotifyAsync(
                        NotificationRecipientType.Client,
                        ticket.ClientId.ToString(),
                        "awaiting_confirmation",
                        $"Ticket {ticket.Id} has been marked resolved — please confirm it's working and rate your experience.",
                        ct);
                }
                catch (Exception)
                {
                    // Swallow: the ticket status is already saved. Losing this
                    // notification is not worth failing the whole request over.
                }
            }
            else
            {
                ticket.Status = request.Status;

                ticket.AuditTrail.Add(
                    new TicketAuditEntry
                    {
                        TicketId = ticket.Id,
                        Actor = request.ActorName,
                        Action =
                            $"Status changed to {request.Status}"
                    });

                await _db.SaveChangesAsync(ct);
            }
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Someone else (a duplicate tap, another tab, another
            // technician) already changed this exact ticket between our
            // read and our save — the 0-rows-affected update is EF
            // reporting a lost race, not a server fault. Surface it as a
            // normal, actionable error instead of a 500. Logged with the
            // conflicting entry types so a *real* recurrence (as opposed
            // to the old false positive from re-attaching the whole
            // graph via _db.Update) can be diagnosed.
            _logger.LogWarning(
                ex,
                "Concurrency conflict updating ticket {TicketId}. Conflicting entries: {Entries}",
                ticketId,
                string.Join(", ", ex.Entries.Select(e => e.Entity.GetType().Name)));

            throw new InvalidOperationException(
                "This ticket was just updated by someone else — refresh the page and try again.");
        }

        return await LoadDtoAsync(ticket.Id, ct);
    }

    public async Task<TicketDto> ConfirmResolutionAsync(
        Guid ticketId,
        ClientConfirmationRequest request,
        CancellationToken ct = default)
    {
        var ticket = await _db.Tickets
            .FirstOrDefaultAsync(t => t.Id == ticketId, ct)
            ?? throw new InvalidOperationException(
                "Ticket not found.");

        if (ticket.Status !=
            TicketStatus.AwaitingClientConfirmation)
        {
            throw new InvalidOperationException(
                "This ticket is not currently awaiting client confirmation.");
        }

        if (!request.IsFixed)
        {
            ticket.Status = TicketStatus.InProgress;
            ticket.ResolvedAt = null;
            ticket.ClientConfirmationDeadline = null;

            ticket.AuditTrail.Add(
                new TicketAuditEntry
                {
                    TicketId = ticket.Id,
                    Actor = "Client",
                    Action =
                        "Reported issue is NOT fixed — reopened to assigned employee"
                });

            _db.Update(ticket);

            await _db.SaveChangesAsync(ct);

            if (ticket.AssignedEmployeeId is { } reopenedEmpId)
            {
                await _notifications.NotifyAsync(
                    NotificationRecipientType.Employee,
                    reopenedEmpId.ToString(),
                    "ticket_reopened",
                    $"Ticket {ticket.Id} was reopened — the client says it isn't fixed yet.",
                    ct);
            }

            await _notifications.NotifyAsync(
                NotificationRecipientType.Admin,
                "ALL_ADMIN",
                "ticket_reopened",
                $"Ticket {ticket.Id} reopened — client reported it's not fixed.",
                ct);

            return await LoadDtoAsync(ticket.Id, ct);
        }

        if (request.SatisfactionStars is not (>= 1 and <= 5))
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "Satisfaction rating must be between 1 and 5 stars.");
        }

        var stars = request.SatisfactionStars!.Value;
        var score = stars * 20;

        ticket.SatisfactionStars = stars;
        ticket.SatisfactionScore = score;

        var minimumSatisfactionScore =
            await _config.GetIntAsync(
                "TicketWorkflow.MinimumSatisfactionScore",
                ct);

        if (score >= minimumSatisfactionScore)
        {
            ticket.Status = TicketStatus.Closed;
            ticket.ClosureReason =
                ClosureReason.ClientConfirmedSatisfied;
            ticket.ClosedAt =
                DateTimeOffset.UtcNow;

            ticket.AuditTrail.Add(
                new TicketAuditEntry
                {
                    TicketId = ticket.Id,
                    Actor = "Client",
                    Action =
                        $"Confirmed fixed and rated {stars}★ ({score}/100). Closed."
                });

            _db.Update(ticket);

            await _db.SaveChangesAsync(ct);

            await _notifications.NotifyAsync(
                NotificationRecipientType.Admin,
                "ALL_ADMIN",
                "ticket_closed",
                $"Ticket {ticket.Id} closed — {score}/100 satisfaction.",
                ct);

            if (ticket.AssignedEmployeeId is { } empId)
            {
                await _notifications.NotifyAsync(
                    NotificationRecipientType.Employee,
                    empId.ToString(),
                    "ticket_closed",
                    $"Ticket {ticket.Id} closed — client rated {score}/100.",
                    ct);
            }
        }
        else
        {
            ticket.Status = TicketStatus.Escalated;

            ticket.AuditTrail.Add(
                new TicketAuditEntry
                {
                    TicketId = ticket.Id,
                    Actor = "Client",
                    Action =
                        $"Confirmed fixed but rated {stars}★ ({score}/100) — below threshold, escalated to Admin"
                });

            _db.Update(ticket);

            await _db.SaveChangesAsync(ct);

            await _notifications.NotifyAsync(
                NotificationRecipientType.Admin,
                "ALL_ADMIN",
                "ticket_escalated",
                $"Ticket {ticket.Id} escalated — client rated it {score}/100.",
                ct);
        }

        return await LoadDtoAsync(ticket.Id, ct);
    }

    public async Task<int> AutoCloseUnansweredTicketsAsync(
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        var overdue = await _db.Tickets
            .Where(
                t =>
                    t.Status ==
                    TicketStatus.AwaitingClientConfirmation
                    && t.ClientConfirmationDeadline != null
                    && t.ClientConfirmationDeadline <= now)
            .ToListAsync(ct);

        var confirmationWindowDays =
            await _config.GetIntAsync(
                "TicketWorkflow.ClientConfirmationWindowDays",
                ct);

        foreach (var ticket in overdue)
        {
            ticket.Status = TicketStatus.Closed;
            ticket.ClosureReason =
                ClosureReason.AutoClosedNoResponse;
            ticket.ClosedAt = now;

            ticket.AuditTrail.Add(
                new TicketAuditEntry
                {
                    TicketId = ticket.Id,
                    Actor = "System",
                    Action =
                        $"Auto-closed after {confirmationWindowDays} days with no client response"
                });

            _db.Update(ticket);

            await _notifications.NotifyAsync(
                NotificationRecipientType.Client,
                ticket.ClientId.ToString(),
                "ticket_autoclosed",
                $"Ticket {ticket.Id} was automatically closed after no response — assumed resolved.",
                ct);
        }

        if (overdue.Count > 0)
            await _db.SaveChangesAsync(ct);

        return overdue.Count;
    }

    public async Task<IReadOnlyList<TicketDto>> GetAllAsync(
        CancellationToken ct = default) =>
        await ProjectAsync(_db.Tickets, ct);

    public async Task<PagedResult<TicketDto>> GetAllPagedAsync(
        PaginationQuery query,
        Guid? assignedEmployeeId = null,
        CancellationToken ct = default)
    {
        // assignedEmployeeId is set by the controller (from the caller's own
        // JWT) whenever the caller isn't an Admin, so a technician only ever
        // gets tickets assigned to them — regardless of what query params a
        // client sends.
        var baseQuery = assignedEmployeeId is Guid empId
            ? _db.Tickets.Where(t => t.AssignedEmployeeId == empId)
            : _db.Tickets;

        var totalCount =
            await baseQuery.CountAsync(ct);

        var page = await baseQuery
            .AsNoTracking()
            .Include(t => t.Client)
            .Include(t => t.AssignedEmployee)
            .Include(t => t.AuditTrail)
            .Include(t => t.FailureType)
            .OrderByDescending(t => t.DateSubmitted)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync(ct);

        var items = page.Select(
            t =>
                new TicketDto(
                    t.Id,
                    t.ClientId,
                    t.Client.Name,
                    t.AgreementId,
                    t.Description,
                    t.Category,
                    t.FailureTypeId,
                    t.FailureType?.Name,
                    t.DateSubmitted,
                    t.ForwardedByEmployeeId,
                    t.AssignedEmployeeId,
                    t.AssignedEmployee?.FullName,
                    t.AssignedAt,
                    ExpectedResolutionBy(t),
                    t.Chargeable,
                    t.Status,
                    t.ResolvedAt,
                    t.ClientConfirmationDeadline,
                    t.SatisfactionStars,
                    t.SatisfactionScore,
                    t.ClosureReason,
                    t.AttachmentFileName,
                    t.VoiceNoteFileName,
                    t.AuditTrail
                        .OrderBy(a => a.Timestamp)
                        .Select(
                            a =>
                                new TicketAuditEntryDto(
                                    a.Timestamp,
                                    a.Actor,
                                    a.Action))
                        .ToList()
                ))
            .ToList();

        return new PagedResult<TicketDto>(
            items,
            query.Page,
            query.PageSize,
            totalCount);
    }

    public async Task<TicketDto?> GetByIdAsync(
        Guid id,
        CancellationToken ct = default) =>
        (
            await ProjectAsync(
                _db.Tickets.Where(t => t.Id == id),
                ct)
        ).FirstOrDefault();

    public async Task<IReadOnlyList<TicketDto>> GetForClientAsync(
        Guid clientId,
        CancellationToken ct = default) =>
        await ProjectAsync(
            _db.Tickets.Where(t => t.ClientId == clientId),
            ct);

    public async Task<IReadOnlyList<TicketDto>> GetForEmployeeAsync(
        Guid employeeId,
        CancellationToken ct = default) =>
        await ProjectAsync(
            _db.Tickets.Where(
                t => t.AssignedEmployeeId == employeeId),
            ct);

    public async Task<IReadOnlyList<TicketDto>>
        GetAwaitingConfirmationForClientAsync(
            Guid clientId,
            CancellationToken ct = default) =>
        await ProjectAsync(
            _db.Tickets.Where(
                t =>
                    t.ClientId == clientId &&
                    t.Status ==
                        TicketStatus.AwaitingClientConfirmation),
            ct);

    public async Task<IReadOnlyList<TicketDto>> GetEscalatedAsync(
        CancellationToken ct = default) =>
        await ProjectAsync(
            _db.Tickets.Where(
                t => t.Status == TicketStatus.Escalated),
            ct);

    private async Task<TicketDto> LoadDtoAsync(
        Guid id,
        CancellationToken ct) =>
        (
            await ProjectAsync(
                _db.Tickets.Where(t => t.Id == id),
                ct)
        ).First();

    private static async Task<IReadOnlyList<TicketDto>> ProjectAsync(
        IQueryable<Ticket> query,
        CancellationToken ct)
    {
        var tickets = await query
            .AsNoTracking()
            .Include(t => t.Client)
            .Include(t => t.AssignedEmployee)
            .Include(t => t.AuditTrail)
            .Include(t => t.FailureType)
            .OrderByDescending(t => t.DateSubmitted)
            .ToListAsync(ct);

        return tickets.Select(
            t =>
                new TicketDto(
                    t.Id,
                    t.ClientId,
                    t.Client.Name,
                    t.AgreementId,
                    t.Description,
                    t.Category,
                    t.FailureTypeId,
                    t.FailureType?.Name,
                    t.DateSubmitted,
                    t.ForwardedByEmployeeId,
                    t.AssignedEmployeeId,
                    t.AssignedEmployee?.FullName,
                    t.AssignedAt,
                    ExpectedResolutionBy(t),
                    t.Chargeable,
                    t.Status,
                    t.ResolvedAt,
                    t.ClientConfirmationDeadline,
                    t.SatisfactionStars,
                    t.SatisfactionScore,
                    t.ClosureReason,
                    t.AttachmentFileName,
                    t.VoiceNoteFileName,
                    t.AuditTrail
                        .OrderBy(a => a.Timestamp)
                        .Select(
                            a =>
                                new TicketAuditEntryDto(
                                    a.Timestamp,
                                    a.Actor,
                                    a.Action))
                        .ToList()
                ))
            .ToList();
    }

    private static DateTimeOffset? ExpectedResolutionBy(
        Ticket t) =>
        t.AssignedAt is null || t.FailureType is null
            ? null
            : t.AssignedAt.Value + t.FailureType.ToTimeSpan();

    public async Task<TicketDto> UploadAttachmentAsync(
        Guid ticketId,
        Stream content,
        string fileName,
        string contentType,
        CancellationToken ct = default)
    {
        var ticket = await _db.Tickets
            .FirstOrDefaultAsync(
                t => t.Id == ticketId,
                ct)
            ?? throw new InvalidOperationException(
                "Ticket not found.");

        if (!string.IsNullOrEmpty(
            ticket.AttachmentStorageKey))
        {
            await _storage.DeleteAsync(
                ticket.AttachmentStorageKey,
                ct);
        }

        var result = await _storage.SaveAsync(
            content,
            fileName,
            contentType,
            ct);

        ticket.AttachmentStorageKey =
            result.StorageKey;

        ticket.AttachmentFileName =
            result.OriginalFileName;

        _db.Update(ticket);

        await _db.SaveChangesAsync(ct);

        return await LoadDtoAsync(ticketId, ct);
    }

    public async Task<RetrievedFile?> DownloadAttachmentAsync(
        Guid ticketId,
        CancellationToken ct = default)
    {
        var ticket = await _db.Tickets
            .AsNoTracking()
            .FirstOrDefaultAsync(
                t => t.Id == ticketId,
                ct);

        if (
            ticket is null ||
            string.IsNullOrEmpty(
                ticket.AttachmentStorageKey))
        {
            return null;
        }

        return await _storage.GetAsync(
            ticket.AttachmentStorageKey,
            ct);
    }

    public async Task<bool> CanAccessAttachmentAsync(
        Guid ticketId,
        SessionAccountType callerType,
        Guid callerId,
        CancellationToken ct = default)
    {
        var ticket = await _db.Tickets
            .AsNoTracking()
            .FirstOrDefaultAsync(
                t => t.Id == ticketId,
                ct);

        if (ticket is null)
            return false;

        if (callerType == SessionAccountType.Client)
            return ticket.ClientId == callerId;

        if (ticket.AssignedEmployeeId == callerId)
            return true;

        var employee = await _db.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(
                e => e.Id == callerId,
                ct);

        return employee is not null &&
               employee.Roles.Any(
                   r => r == EmployeeRole.Admin);
    }

    public async Task<(
        string StorageKey,
        string FileName)> UploadVoiceNoteAsync(
        Stream content,
        string fileName,
        string contentType,
        CancellationToken ct = default)
    {
        var result = await _storage.SaveAsync(
            content,
            fileName,
            contentType,
            ct);

        return (
            result.StorageKey,
            result.OriginalFileName);
    }

    public async Task<RetrievedFile?> DownloadVoiceNoteAsync(
        Guid ticketId,
        CancellationToken ct = default)
    {
        var ticket = await _db.Tickets
            .AsNoTracking()
            .FirstOrDefaultAsync(
                t => t.Id == ticketId,
                ct);

        if (
            ticket is null ||
            string.IsNullOrEmpty(
                ticket.VoiceNoteStorageKey))
        {
            return null;
        }

        return await _storage.GetAsync(
            ticket.VoiceNoteStorageKey,
            ct);
    }
}