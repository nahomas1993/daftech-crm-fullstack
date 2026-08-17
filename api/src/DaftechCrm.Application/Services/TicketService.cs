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

            if (request.FailureTypeId is Guid failureTypeId)
            {
                var failureType = await _db.FailureTypes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(f => f.Id == failureTypeId, ct);

                if (failureType is not null)
                {
                    // Snapshot the duration now — later admin edits to this
                    // FailureType must not change this ticket's SLA.
                    ticket.ExpectedResolutionMinutes =
                        (int)failureType.ToTimeSpan().TotalMinutes;

                    ticket.ExpectedResolutionBy =
                        ticket.AssignedAt.Value.AddMinutes(
                            ticket.ExpectedResolutionMinutes.Value);
                }
            }

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

    /// <summary>
    /// Applies a technician's status change to a ticket.
    ///
    /// Flow: find by ID (404 if missing), verify the caller is the assigned
    /// technician (Admins may update any ticket), apply the change, save,
    /// then notify.
    ///
    /// Two things that used to break "Resolved" are handled here:
    ///
    /// 1. Ticket no longer carries an xmin concurrency token (see
    ///    TicketConfiguration), so the UPDATE is a plain update-by-id and can
    ///    no longer fail as a phantom 409 just because the row was touched
    ///    between the read and the save. Should any concurrency exception
    ///    still surface (e.g. another writer deleted the row), we reload once
    ///    and retry rather than dumping "changed by another user" on the
    ///    technician.
    /// 2. The move to Resolved is idempotent: if the ticket is already
    ///    AwaitingClientConfirmation (or beyond), the call succeeds and simply
    ///    returns the current ticket instead of erroring, so a retry after a
    ///    flaky response never leaves the UI stuck on a red message.
    /// </summary>
    public async Task<TicketDto> UpdateStatusAsync(
        Guid ticketId,
        UpdateTicketStatusRequest request,
        SessionAccountType callerType,
        Guid callerId,
        CancellationToken ct = default)
    {
        var ticket = await _db.Tickets
            .FirstOrDefaultAsync(t => t.Id == ticketId, ct)
            ?? throw new TicketNotFoundException(ticketId);

        if (callerType == SessionAccountType.Employee &&
            ticket.AssignedEmployeeId != callerId)
        {
            var caller = await _db.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == callerId, ct);

            var isAdmin = caller is not null &&
                caller.Roles.Any(r => r == EmployeeRole.Admin);

            if (!isAdmin)
            {
                throw new UnauthorizedAccessException(
                    "You are not the technician assigned to this ticket.");
            }
        }

        var wantsResolved = request.Status == TicketStatus.Resolved;

        // Idempotent no-op: the resolve already landed (possibly on a previous
        // attempt whose response the browser never saw). Report success.
        if (wantsResolved &&
            (ticket.Status == TicketStatus.AwaitingClientConfirmation ||
             ticket.Status == TicketStatus.Closed ||
             ticket.Status == TicketStatus.Escalated))
        {
            return await LoadDtoAsync(ticket.Id, ct);
        }

        if (!wantsResolved && ticket.Status == request.Status)
        {
            return await LoadDtoAsync(ticket.Id, ct);
        }

        var notifyClientOfResolution = false;

        if (wantsResolved)
        {
            ticket.Status = TicketStatus.AwaitingClientConfirmation;
            ticket.ResolvedAt = DateTimeOffset.UtcNow;

            // A missing/zero/garbage setting used to produce a deadline of
            // "right now", which the 15-minute auto-close sweep then closed
            // immediately — the client never got a chance to confirm. Fall
            // back to the documented 5-day window instead.
            var confirmationWindowDays =
                await _config.GetIntAsync(
                    "TicketWorkflow.ClientConfirmationWindowDays",
                    ct);

            if (confirmationWindowDays <= 0)
            {
                _logger.LogWarning(
                    "TicketWorkflow.ClientConfirmationWindowDays resolved to {Days}; falling back to 5 days.",
                    confirmationWindowDays);

                confirmationWindowDays = 5;
            }

            ticket.ClientConfirmationDeadline =
                ticket.ResolvedAt.Value.AddDays(confirmationWindowDays);

            ticket.AuditTrail.Add(
                new TicketAuditEntry
                {
                    TicketId = ticket.Id,
                    Actor = request.ActorName,
                    Action =
                        $"Marked Resolved by {request.ActorName}; awaiting client confirmation (deadline {ticket.ClientConfirmationDeadline:u})"
                });

            notifyClientOfResolution = true;
        }
        else
        {
            ticket.Status = request.Status;

            ticket.AuditTrail.Add(
                new TicketAuditEntry
                {
                    TicketId = ticket.Id,
                    Actor = request.ActorName,
                    Action = $"Status changed to {request.Status}"
                });
        }

        var targetStatus = ticket.Status;
        var resolvedAt = ticket.ResolvedAt;
        var deadline = ticket.ClientConfirmationDeadline;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(
                ex,
                "Concurrency exception updating ticket {TicketId}; reloading and retrying once.",
                ticketId);

            _db.Detach(ticket);

            var fresh = await _db.Tickets
                .FirstOrDefaultAsync(t => t.Id == ticketId, ct)
                ?? throw new TicketNotFoundException(ticketId);

            // Someone else already put it where we wanted it — nothing to do.
            if (fresh.Status == targetStatus)
            {
                return await LoadDtoAsync(fresh.Id, ct);
            }

            fresh.Status = targetStatus;

            if (wantsResolved)
            {
                fresh.ResolvedAt = resolvedAt;
                fresh.ClientConfirmationDeadline = deadline;
            }

            fresh.AuditTrail.Add(
                new TicketAuditEntry
                {
                    TicketId = fresh.Id,
                    Actor = request.ActorName,
                    Action = wantsResolved
                        ? $"Marked Resolved by {request.ActorName}; awaiting client confirmation (deadline {deadline:u})"
                        : $"Status changed to {targetStatus}"
                });

            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException retryEx)
            {
                _logger.LogError(
                    retryEx,
                    "Concurrency conflict persisted on retry for ticket {TicketId}",
                    ticketId);

                throw new ConcurrencyConflictException(
                    "This ticket was just updated by someone else — refresh the page and try again.");
            }

            ticket = fresh;
        }

        if (notifyClientOfResolution)
        {
            // The status change is already committed — a notification failure
            // must never surface as a failed status update.
            try
            {
                await _notifications.NotifyAsync(
                    NotificationRecipientType.Client,
                    ticket.ClientId.ToString(),
                    "awaiting_confirmation",
                    $"Ticket {ticket.Id} has been marked resolved — please confirm it's working and rate your experience.",
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to notify client for resolved ticket {TicketId}", ticket.Id);
            }
        }

        return await LoadDtoAsync(ticket.Id, ct);
    }

    /// <summary>
    /// Records the client's response to a Resolved ticket. Only the
    /// ticket's own client may confirm it — enforced here from the
    /// caller's own JWT (see CallerIdentity), not trusted from anything
    /// the client could send in the request body, matching the same
    /// pattern used in UpdateStatusAsync for technicians.
    /// </summary>
    public async Task<TicketDto> ConfirmResolutionAsync(
        Guid ticketId,
        ClientConfirmationRequest request,
        Guid callerId,
        CancellationToken ct = default)
    {
        var ticket = await _db.Tickets
            .FirstOrDefaultAsync(t => t.Id == ticketId, ct)
            ?? throw new TicketNotFoundException(ticketId);

        if (ticket.ClientId != callerId)
        {
            throw new UnauthorizedAccessException(
                "This ticket does not belong to your account.");
        }

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
        // Use the snapshot taken at assignment time, not a live
        // recalculation off FailureType's current duration (see
        // Ticket.ExpectedResolutionBy for why). Older tickets assigned
        // before this field existed simply have no SLA deadline shown,
        // rather than one silently backfilled from today's FailureType
        // settings.
        t.ExpectedResolutionBy;

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