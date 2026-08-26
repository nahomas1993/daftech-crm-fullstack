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
    private readonly IEthiopianTimeService _officeTime;
    private readonly ILogger<TicketService> _logger;

    public TicketService(
        IAppDbContext db,
        ITicketAssignmentService assignment,
        INotificationService notifications,
        ISystemConfigurationService config,
        IFileStorageService storage,
        IEthiopianTimeService officeTime,
        ILogger<TicketService> logger)
    {
        _db = db;
        _assignment = assignment;
        _notifications = notifications;
        _config = config;
        _storage = storage;
        _officeTime = officeTime;
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
            SupportTypeId = request.SupportTypeId,
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

        // Office-hours-aware assignment: a ticket is only handed to a
        // technician during working hours (see IEthiopianTimeService). One
        // submitted during lunch or after close is still accepted and
        // saved immediately — DateSubmitted is untouched, preserving the
        // real submission time for audit/reporting — it just stays
        // Status=Submitted with no assignee until a working moment is
        // reached, at which point TicketAssignmentSweepHostedService picks
        // it up. DateSubmitted itself is never used to decide assignment
        // timing; "now" (DateTimeOffset.UtcNow) is.
        var now = DateTimeOffset.UtcNow;

        FailureType? failureType = null;
        if (request.FailureTypeId is Guid requestedFailureTypeId)
        {
            failureType = await _db.FailureTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.Id == requestedFailureTypeId, ct);
            if (failureType is null)
                throw new ValidationException("Selected failure type was not found.");
            if (failureType.Category != request.Category)
                throw new ValidationException("Selected failure type does not belong to the selected category.");
        }

        SupportType? supportType = null;
        if (request.SupportTypeId is Guid requestedSupportTypeId)
        {
            supportType = await _db.SupportTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == requestedSupportTypeId, ct);
            if (supportType is null)
                throw new ValidationException("We couldn't find the support type you picked.");
        }

        // Price the ticket here, on the server, from the same numbers the
        // quote endpoint used — never from anything the browser sent. A
        // client whose free support window has run out has to tick the
        // acknowledgement box, so nobody ends up billed for something they
        // didn't agree to.
        if (chargeable)
        {
            if (!request.AcknowledgeChargeable)
                throw new ValidationException(
                    "This request falls outside your free support period, so please confirm you accept the charge before submitting.");

            ticket.ChargeAmount = (failureType?.BasePrice ?? 0m) + (supportType?.AdditionalFee ?? 0m);
            ticket.ChargeAcknowledged = true;
            ticket.AuditTrail.Add(
                new TicketAuditEntry
                {
                    TicketId = ticket.Id,
                    Actor = "Client",
                    Action = $"Accepted a charge of {ticket.ChargeAmount:0.##} ETB for this request"
                });
        }
        else
        {
            ticket.ChargeAmount = null;
            ticket.ChargeAcknowledged = false;
        }

        var canAssignNow = _officeTime.IsWorkingMoment(now) && FitsBeforeCloseIfSaturday(now, failureType);

        Employee? assignee = null;
        var queuedForOfficeHours = false;

        if (canAssignNow)
        {
            assignee =
                await _assignment.SelectAssigneeAsync(ct);

            if (assignee is not null)
            {
                ticket.AssignedEmployeeId = assignee.Id;
                ticket.AssignedAt = now;
                ticket.Status = TicketStatus.Assigned;

                if (failureType is not null)
                {
                    // Snapshot the duration now — later admin edits to this
                    // FailureType must not change this ticket's SLA.
                    ticket.ExpectedResolutionMinutes =
                        (int)failureType.ToTimeSpan().TotalMinutes;

                    // Working-minutes deadline, not wall-clock — skips
                    // lunch/off-hours/weekends so a ticket assigned at
                    // 11:00 with a 2-hour SLA doesn't get an unreachable
                    // same-day deadline.
                    ticket.ExpectedResolutionBy =
                        _officeTime.AddWorkingMinutes(ticket.AssignedAt.Value, ticket.ExpectedResolutionMinutes.Value);
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
        }
        else
        {
            queuedForOfficeHours = true;
            var nextAssignable = _officeTime.NextAssignableMoment(now);
            ticket.AuditTrail.Add(
                new TicketAuditEntry
                {
                    TicketId = ticket.Id,
                    Actor = "System",
                    Action =
                        $"Received outside office hours — queued for assignment at {nextAssignable.ToOffset(TimeSpan.FromHours(3)):yyyy-MM-dd HH:mm} local time"
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
        else if (queuedForOfficeHours)
        {
            // Not a failure — just queued. No admin alert needed; the
            // sweep will assign it once office hours resume. Avoids
            // paging Admins every time a ticket lands overnight.
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
    /// Saturday is a half-day (8:30 AM-12:30 PM, no lunch) — a ticket is only
    /// assigned immediately on a Saturday if its FailureType's expected
    /// duration fits before 6:00 close; otherwise it queues for Monday
    /// 8:30 AM even though it's technically still "office hours" right
    /// now. Any other working day: always fits (no early-close
    /// constraint). No FailureType chosen: always fits — there's no
    /// duration to check against, so the ticket goes to a technician
    /// immediately as before.
    /// </summary>
    private bool FitsBeforeCloseIfSaturday(DateTimeOffset nowUtc, FailureType? failureType)
    {
        var nowLocal = nowUtc.ToOffset(TimeSpan.FromHours(3));
        if (nowLocal.DayOfWeek != DayOfWeek.Saturday || failureType is null)
            return true;

        var minutesNeeded = (int)failureType.ToTimeSpan().TotalMinutes;
        var deadlineIfAssignedNow = _officeTime.AddWorkingMinutes(nowUtc, minutesNeeded);
        var deadlineLocal = deadlineIfAssignedNow.ToOffset(TimeSpan.FromHours(3));

        // AddWorkingMinutes rolls over to Monday once Saturday's close is
        // reached — if the computed deadline landed on the same Saturday
        // calendar day as "now", the work fit before close.
        return deadlineLocal.Date == nowLocal.Date;
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

        var auditAction = wantsResolved
            ? $"Marked Resolved by {request.ActorName}; awaiting client confirmation (deadline {deadline:u})"
            : $"Status changed to {targetStatus}";

        // Bounded reload-and-retry loop. Every attempt re-reads the row so the
        // write is always applied on top of the LATEST ticket data, and any
        // half-applied tracked state (the ticket plus the audit entry we just
        // added) is detached first so a retry can never insert a duplicate
        // audit row. Only if every attempt loses the race do we surface a
        // conflict — a transient overlap with the auto-close sweep, a second
        // tab, or a polling refresh resolves silently instead of showing the
        // technician a red "changed by another user" message.
        const int maxAttempts = 4;

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await _db.SaveChangesAsync(ct);
                break;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (attempt >= maxAttempts)
                {
                    _logger.LogError(
                        ex,
                        "Concurrency conflict persisted after {Attempts} attempts for ticket {TicketId}",
                        attempt,
                        ticketId);

                    // Last word goes to the database, not to the failed
                    // write: if the ticket already sits at the status we
                    // wanted, the intent is satisfied and this is a success.
                    // Only a row that genuinely still differs is a conflict.
                    var authoritative = await _db.Tickets
                        .AsNoTracking()
                        .FirstOrDefaultAsync(t => t.Id == ticketId, ct);

                    if (authoritative is not null && authoritative.Status == targetStatus)
                    {
                        return await LoadDtoAsync(ticketId, ct);
                    }

                    throw new ConcurrencyConflictException(
                        "This ticket was just updated by someone else — refresh the page and try again.");
                }

                _logger.LogWarning(
                    ex,
                    "Concurrency exception updating ticket {TicketId} (attempt {Attempt}); reloading latest data and retrying.",
                    ticketId,
                    attempt);

                // Drop the stale tracked graph: the ticket AND the audit entry
                // queued against it, so the retry starts from a clean slate.
                foreach (var pending in ticket.AuditTrail.ToList())
                {
                    _db.Detach(pending);
                }

                _db.Detach(ticket);

                var fresh = await _db.Tickets
                    .FirstOrDefaultAsync(t => t.Id == ticketId, ct)
                    ?? throw new TicketNotFoundException(ticketId);

                // Someone else already put it where we wanted it — the intent
                // is satisfied, so this is a success, not a conflict.
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
                        Action = auditAction
                    });

                ticket = fresh;
            }
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

    /// <summary>See ITicketService.SetPriorityAsync. A plain field update — no status/audit-trail interaction, no notification, unlike UpdateStatusAsync above.</summary>
    public async Task<TicketDto> SetPriorityAsync(Guid ticketId, SetTicketPriorityRequest request, CancellationToken ct = default)
    {
        var ticket = await _db.Tickets.FirstOrDefaultAsync(t => t.Id == ticketId, ct)
            ?? throw new InvalidOperationException("Ticket not found.");

        ticket.Priority = request.Priority;
        _db.Update(ticket);
        await _db.SaveChangesAsync(ct);

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

        if (request.SatisfactionStars is not (>= 1 and <= 5) ||
            (request.SatisfactionStars.Value * 2) % 1 != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "Satisfaction rating must be between 1 and 5 stars, in half-star increments (e.g. 3.5).");
        }

        var stars = request.SatisfactionStars!.Value;
        var score = (int)(stars * 20);

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

    /// <summary>
    /// See ITicketService.AssignQueuedTicketsAsync. Only does work at all
    /// if we're currently in a working moment — checked once up front so a
    /// sweep tick that lands during lunch/off-hours is a fast no-op rather
    /// than repeatedly re-checking IsWorkingMoment per ticket for no
    /// reason (they'd all be false anyway).
    /// </summary>
    public async Task<int> AssignQueuedTicketsAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        if (!_officeTime.IsWorkingMoment(now))
            return 0;

        var queued = await _db.Tickets
            .Where(t => t.Status == TicketStatus.Submitted && t.AssignedEmployeeId == null)
            .OrderBy(t => t.DateSubmitted) // Oldest-queued gets assigned first within this sweep.
            .ToListAsync(ct);

        var assignedCount = 0;

        foreach (var ticket in queued)
        {
            // Re-fetch the FailureType per ticket (not batched) — sweeps run
            // at most every few minutes and the queue is expected to be
            // small (only tickets that arrived outside office hours), so
            // the extra round trips are not a real cost here.
            FailureType? failureType = ticket.FailureTypeId is Guid ftId
                ? await _db.FailureTypes.AsNoTracking().FirstOrDefaultAsync(f => f.Id == ftId, ct)
                : null;

            if (!FitsBeforeCloseIfSaturday(now, failureType))
                continue; // Still queued — will pick up Monday's sweep.

            var assignee = await _assignment.SelectAssigneeAsync(ct);
            if (assignee is null)
                break; // No eligible technician at all right now — nothing else in the queue will fare better this tick.

            ticket.AssignedEmployeeId = assignee.Id;
            ticket.AssignedAt = now;
            ticket.Status = TicketStatus.Assigned;

            if (failureType is not null)
            {
                ticket.ExpectedResolutionMinutes = (int)failureType.ToTimeSpan().TotalMinutes;
                ticket.ExpectedResolutionBy = _officeTime.AddWorkingMinutes(now, ticket.ExpectedResolutionMinutes.Value);
            }

            ticket.AuditTrail.Add(new TicketAuditEntry
            {
                TicketId = ticket.Id,
                Actor = "System",
                Action = $"Auto-assigned to {assignee.FullName} once office hours resumed (lowest open-ticket count)",
            });

            _db.Update(ticket);
            assignedCount++;

            await _notifications.NotifyAsync(NotificationRecipientType.Employee, assignee.Id.ToString(), "ticket_assigned",
                $"You were assigned ticket {ticket.Id}.", ct);
            await _notifications.NotifyAsync(NotificationRecipientType.Client, ticket.ClientId.ToString(), "ticket_assigned",
                $"Your ticket {ticket.Id} has been assigned to a technician.", ct);
        }

        if (assignedCount > 0)
            await _db.SaveChangesAsync(ct);

        return assignedCount;
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
            .Include(t => t.SupportType)
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
                    t.SupportTypeId,
                    t.SupportType?.Name,
                    t.DateSubmitted,
                    t.ForwardedByEmployeeId,
                    t.AssignedEmployeeId,
                    t.AssignedEmployee?.FullName,
                    t.AssignedAt,
                    ExpectedResolutionBy(t),
                    t.Chargeable,
                    t.ChargeAmount,
                    t.ChargeAcknowledged,
                    t.Status,
                    t.Priority,
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
            .Include(t => t.SupportType)
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
                    t.SupportTypeId,
                    t.SupportType?.Name,
                    t.DateSubmitted,
                    t.ForwardedByEmployeeId,
                    t.AssignedEmployeeId,
                    t.AssignedEmployee?.FullName,
                    t.AssignedAt,
                    ExpectedResolutionBy(t),
                    t.Chargeable,
                    t.ChargeAmount,
                    t.ChargeAcknowledged,
                    t.Status,
                    t.Priority,
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

        // Save the new file BEFORE touching the old one. Deleting first
        // (the previous order) meant a failed SaveAsync — validation
        // error, storage outage, whatever — left the ticket with its old
        // attachment gone and nothing to replace it, i.e. silent data
        // loss. Saving first means a failed upload leaves the existing
        // attachment untouched; only a successful save can displace it.
        var result = await _storage.SaveAsync(
            content,
            fileName,
            contentType,
            ct);

        var previousStorageKey = ticket.AttachmentStorageKey;

        ticket.AttachmentStorageKey =
            result.StorageKey;

        ticket.AttachmentFileName =
            result.OriginalFileName;

        _db.Update(ticket);

        await _db.SaveChangesAsync(ct);

        // Only remove the old file once the new one is safely referenced
        // by the ticket. Best-effort: if the old file is already gone or
        // the delete fails, the ticket has already moved on to the new
        // attachment, so this must never fail the whole upload.
        if (!string.IsNullOrEmpty(previousStorageKey))
        {
            try
            {
                await _storage.DeleteAsync(
                    previousStorageKey,
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to delete superseded attachment {StorageKey} for ticket {TicketId} — the ticket now points at its new attachment regardless.",
                    previousStorageKey,
                    ticketId);
            }
        }

        return await LoadDtoAsync(ticketId, ct);
    }

    public async Task<FileRetrievalResult> DownloadAttachmentAsync(
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
            return FileRetrievalResult.NoFile();
        }

        var file = await _storage.GetAsync(
            ticket.AttachmentStorageKey,
            ct);

        if (file is null)
        {
            _logger.LogWarning(
                "Ticket {TicketId} has AttachmentStorageKey {StorageKey} on record, but the storage backend could not find it.",
                ticketId,
                ticket.AttachmentStorageKey);
            return FileRetrievalResult.Lost();
        }

        return FileRetrievalResult.Found(file);
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

    public async Task<FileRetrievalResult> DownloadVoiceNoteAsync(
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
            return FileRetrievalResult.NoFile();
        }

        var file = await _storage.GetAsync(
            ticket.VoiceNoteStorageKey,
            ct);

        if (file is null)
        {
            _logger.LogWarning(
                "Ticket {TicketId} has VoiceNoteStorageKey {StorageKey} on record, but the storage backend could not find it.",
                ticketId,
                ticket.VoiceNoteStorageKey);
            return FileRetrievalResult.Lost();
        }

        return FileRetrievalResult.Found(file);
    }

    /// <summary>
    /// Works out what a ticket would cost before the client commits to it.
    /// Free while the agreement's support window is still open; after that
    /// it's the failure type's base price plus the support type's fee.
    /// </summary>
    public async Task<TicketQuoteDto> QuoteAsync(
        Guid agreementId,
        Guid? failureTypeId,
        Guid? supportTypeId,
        CancellationToken ct = default)
    {
        var agreement = await _db.Agreements
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == agreementId, ct)
            ?? throw new InvalidOperationException("Agreement not found.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var chargeable = !agreement.IsWithinSupportWindow(today);
        var freeSupportEndsOn = agreement.SignDate.AddMonths(agreement.SupportWindowMonths);

        var basePrice = 0m;
        if (failureTypeId is Guid ftId)
            basePrice = await _db.FailureTypes.AsNoTracking()
                .Where(f => f.Id == ftId).Select(f => f.BasePrice).FirstOrDefaultAsync(ct);

        var supportFee = 0m;
        if (supportTypeId is Guid stId)
            supportFee = await _db.SupportTypes.AsNoTracking()
                .Where(s => s.Id == stId).Select(s => s.AdditionalFee).FirstOrDefaultAsync(ct);

        return new TicketQuoteDto(
            chargeable,
            chargeable ? basePrice : 0m,
            chargeable ? supportFee : 0m,
            chargeable ? basePrice + supportFee : 0m,
            freeSupportEndsOn);
    }
}
