using DaftechCrm.Api.Auth;
using DaftechCrm.Application;
using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DaftechCrm.Api.Controllers;

[ApiController]
[Route("api/tickets")]
[Authorize(Policy = AuthorizationPolicies.AnyAuthenticated)]
public class TicketsController : ControllerBase
{
    private readonly ITicketService _tickets;

    public TicketsController(ITicketService tickets)
        => _tickets = tickets;

    /// <summary>
    /// Unscoped full ticket dump — Admin-only. Non-admin technicians must
    /// use GetAllPaged, which scopes results to their own assigned
    /// tickets from the caller's JWT. This endpoint previously had no
    /// role restriction beyond "any employee," which meant any
    /// technician's browser could pull every ticket in the system
    /// (including other technicians' and Admin-only tickets) into memory.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<IReadOnlyList<TicketDto>>> GetAll(
        CancellationToken ct) =>
        Ok(await _tickets.GetAllAsync(ct));

    /// <summary>
    /// Paged ticket listing for the Tickets table
    /// (query: page, pageSize). Non-admin technicians only ever see
    /// tickets assigned to them — enforced here from the caller's own
    /// JWT, not from anything the client could send, so the UI can't be
    /// bypassed by editing the request.
    /// </summary>
    [HttpGet("paged")]
    [Authorize(Policy = AuthorizationPolicies.AnyEmployee)]
    public async Task<ActionResult<PagedResult<TicketDto>>> GetAllPaged(
        [FromQuery] PaginationQuery query,
        CancellationToken ct)
    {
        Guid? scopeToEmployeeId = null;

        if (!User.IsInRole(nameof(EmployeeRole.Admin)))
        {
            var (_, callerId) = CallerIdentity.Resolve(User);
            scopeToEmployeeId = callerId;
        }

        return Ok(
            await _tickets.GetAllPagedAsync(
                query,
                scopeToEmployeeId,
                ct));
    }

    /// <summary>
    /// A client may only fetch their own ticket; any employee may fetch
    /// any ticket (technicians need to look up tickets by id from links,
    /// e.g. in notifications, regardless of assignment — the tickets LIST
    /// is already scoped to "my tickets" for non-admins in GetAllPaged,
    /// this is just a single-record lookup).
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TicketDto>> GetById(
        Guid id,
        CancellationToken ct)
    {
        var ticket =
            await _tickets.GetByIdAsync(id, ct);

        if (ticket is null)
            return NotFound();

        var (callerType, callerId) = CallerIdentity.Resolve(User);
        if (callerType == SessionAccountType.Client && ticket.ClientId != callerId)
            return NotFound();

        return Ok(ticket);
    }

    /// <summary>
    /// A client may only list their own tickets — an Admin/employee
    /// calling this on behalf of a client (e.g. from the Client Detail
    /// page) is allowed through unrestricted, since staff already have
    /// broader ticket access via GetAllPaged.
    /// </summary>
    [HttpGet("client/{clientId:guid}")]
    public async Task<ActionResult<IReadOnlyList<TicketDto>>> GetForClient(
        Guid clientId,
        CancellationToken ct)
    {
        var (callerType, callerId) = CallerIdentity.Resolve(User);
        if (callerType == SessionAccountType.Client && callerId != clientId)
            return this.ForbidOwnership();

        return Ok(await _tickets.GetForClientAsync(
            clientId,
            ct));
    }

    /// <summary>
    /// Non-admin employees may only list their own assigned tickets —
    /// mirrors the same scoping GetAllPaged already applies for the main
    /// Tickets table.
    /// </summary>
    [HttpGet("employee/{employeeId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AnyEmployee)]
    public async Task<ActionResult<IReadOnlyList<TicketDto>>> GetForEmployee(
        Guid employeeId,
        CancellationToken ct)
    {
        if (!User.IsInRole(nameof(EmployeeRole.Admin)))
        {
            var (_, callerId) = CallerIdentity.Resolve(User);
            if (callerId != employeeId)
                return this.ForbidOwnership();
        }

        return Ok(await _tickets.GetForEmployeeAsync(
            employeeId,
            ct));
    }

    /// <summary>A client may only check their own awaiting-confirmation queue.</summary>
    [HttpGet("client/{clientId:guid}/awaiting-confirmation")]
    public async Task<ActionResult<IReadOnlyList<TicketDto>>>
        GetAwaitingConfirmation(
            Guid clientId,
            CancellationToken ct)
    {
        var (callerType, callerId) = CallerIdentity.Resolve(User);
        if (callerType == SessionAccountType.Client && callerId != clientId)
            return this.ForbidOwnership();

        return Ok(await _tickets.GetAwaitingConfirmationForClientAsync(
            clientId,
            ct));
    }

    /// <summary>
    /// Admin review queue for tickets the client rated below
    /// the satisfaction threshold.
    /// </summary>
    [HttpGet("escalated")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<IReadOnlyList<TicketDto>>> GetEscalated(
        CancellationToken ct) =>
        Ok(await _tickets.GetEscalatedAsync(ct));

    /// <summary>
    /// Auto-assigns to the least-loaded active technician immediately
    /// on submission. The client identity always comes from the caller's
    /// own JWT (see CallerIdentity), never from request.ClientId — that
    /// field exists on SubmitTicketRequest only because the frontend
    /// happens to send its own logged-in client's id, not because it's
    /// trusted; without this override, any authenticated client could
    /// submit a ticket under a different client's identity by editing the
    /// request body.
    /// </summary>
    /// <summary>
    /// What this issue would cost if it were submitted right now. The
    /// portal calls this as the client picks a failure type and support
    /// type so it can show either "Free support" or the exact amount,
    /// priced by the server rather than the browser.
    /// </summary>
    [HttpGet("quote")]
    [Authorize(Policy = AuthorizationPolicies.AnyClient)]
    public async Task<ActionResult<TicketQuoteDto>> GetQuote(
        [FromQuery] Guid agreementId,
        [FromQuery] Guid? failureTypeId,
        [FromQuery] Guid? supportTypeId,
        CancellationToken ct)
    {
        try
        {
            return Ok(await _tickets.QuoteAsync(agreementId, failureTypeId, supportTypeId, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.AnyClient)]
    public async Task<ActionResult<TicketDto>> Submit(
        [FromBody] SubmitTicketRequest request,
        CancellationToken ct)
    {
        var (_, callerId) = CallerIdentity.Resolve(User);
        var effectiveRequest = request with { ClientId = callerId };

        try
        {
            var ticket =
                await _tickets.SubmitFromClientAsync(
                    effectiveRequest,
                    ct);

            return CreatedAtAction(
                nameof(GetById),
                new { id = ticket.Id },
                ticket);
        }
        catch (Application.ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Employee updates status. Setting Resolved does not close
    /// the ticket — it starts the client confirmation window.
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = AuthorizationPolicies.AnyEmployee)]
    public async Task<ActionResult<TicketDto>> UpdateStatus(
        Guid id,
        [FromBody] UpdateTicketStatusRequest request,
        CancellationToken ct)
    {
        var (callerType, callerId) = CallerIdentity.Resolve(User);

        try
        {
            return Ok(
                await _tickets.UpdateStatusAsync(
                    id,
                    request,
                    callerType,
                    callerId,
                    ct));
        }
        catch (TicketNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Conflict(ex.Message);
        }
        catch (UnauthorizedAccessException)
        {
            return this.ForbidOwnership();
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Sets a ticket's priority (Low/Medium/High) — Admin-only. Priority is
    /// now normally fixed automatically at submission from the ticket's
    /// failure type (see FailureType.DefaultPriority and
    /// TicketService.SubmitAsync), configured on the Failure Types &amp;
    /// Pricing settings page. This endpoint remains as an Admin override
    /// for an individual ticket; technicians no longer see an editable
    /// priority control on the Tickets page and cannot call this. Feeds
    /// workload-aware Trainer assignment (see TrainerWorkloadService).
    /// </summary>
    [HttpPatch("{id:guid}/priority")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<TicketDto>> SetPriority(Guid id, [FromBody] SetTicketPriorityRequest request, CancellationToken ct)
    {
        try { return Ok(await _tickets.SetPriorityAsync(id, request, ct)); }
        catch (InvalidOperationException ex) { return NotFound(ex.Message); }
    }

    /// <summary>
    /// Client confirms the fix and rates 1-5 stars, in half-star
    /// increments (e.g. 3.5). Score = stars * 20.
    /// </summary>
    [HttpPost("{id:guid}/confirm")]
    [Authorize(Policy = AuthorizationPolicies.AnyClient)]
    public async Task<ActionResult<TicketDto>> Confirm(
        Guid id,
        [FromBody] ClientConfirmationRequest request,
        CancellationToken ct)
    {
        var (_, callerId) = CallerIdentity.Resolve(User);

        try
        {
            return Ok(
                await _tickets.ConfirmResolutionAsync(
                    id,
                    request,
                    callerId,
                    ct));
        }
        catch (TicketNotFoundException ex)
        {
            return NotFound(ex.Message);
        }
        catch (UnauthorizedAccessException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Uploads or replaces the ticket's optional attachment.
    /// </summary>
    [HttpPost("{id:guid}/attachment")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<TicketDto>> UploadAttachment(
        Guid id,
        IFormFile file,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(
                "No file was provided.");

        var (callerType, callerId) =
            CallerIdentity.Resolve(User);

        var access = await _tickets.CanAccessAttachmentAsync(
            id,
            callerType,
            callerId,
            ct);

        if (access == AttachmentAccessResult.Forbidden)
            return this.ForbidOwnership();
        if (access != AttachmentAccessResult.Granted)
            return NotFound();

        try
        {
            await using var stream =
                file.OpenReadStream();

            var dto =
                await _tickets.UploadAttachmentAsync(
                    id,
                    stream,
                    file.FileName,
                    file.ContentType,
                    ct);

            return Ok(dto);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
        catch (FileValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Streams the ticket's attachment back with its original
    /// content type.
    /// </summary>
    [HttpGet("{id:guid}/attachment")]
    public async Task<IActionResult> DownloadAttachment(
        Guid id,
        CancellationToken ct)
    {
        var (callerType, callerId) =
            CallerIdentity.Resolve(User);

        var access = await _tickets.CanAccessAttachmentAsync(
            id,
            callerType,
            callerId,
            ct);

        if (access != AttachmentAccessResult.Granted)
            return AttachmentAccessError(access);

        var result =
            await _tickets.DownloadAttachmentAsync(
                id,
                ct);

        return result.Status switch
        {
            FileRetrievalStatus.Found => File(
                result.File!.Content,
                result.File.ContentType,
                result.File.OriginalFileName),
            FileRetrievalStatus.FileLost => NotFound(
                "This attachment was recorded on the ticket but could not be found in storage."),
            _ => NotFound("This ticket has no attachment."),
        };
    }

    /// <summary>
    /// Uploads a voice-note recording ahead of submitting
    /// the ticket it will belong to.
    /// </summary>
    [HttpPost("voice-note")]
    [Authorize(Policy = AuthorizationPolicies.AnyClient)]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<VoiceNoteUploadResult>>
        UploadVoiceNote(
            IFormFile file,
            CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(
                "No recording was provided.");

        try
        {
            await using var stream =
                file.OpenReadStream();

            var (storageKey, fileName) =
                await _tickets.UploadVoiceNoteAsync(
                    stream,
                    file.FileName,
                    file.ContentType,
                    ct);

            return Ok(
                new VoiceNoteUploadResult(
                    storageKey,
                    fileName));
        }
        catch (FileValidationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Streams the ticket's voice-note recording back.
    /// </summary>
    [HttpGet("{id:guid}/voice-note")]
    public async Task<IActionResult> DownloadVoiceNote(
        Guid id,
        CancellationToken ct)
    {
        var (callerType, callerId) =
            CallerIdentity.Resolve(User);

        var access = await _tickets.CanAccessAttachmentAsync(
            id,
            callerType,
            callerId,
            ct);

        if (access != AttachmentAccessResult.Granted)
            return AttachmentAccessError(access);

        var result =
            await _tickets.DownloadVoiceNoteAsync(
                id,
                ct);

        return result.Status switch
        {
            FileRetrievalStatus.Found => File(
                result.File!.Content,
                result.File.ContentType,
                result.File.OriginalFileName),
            FileRetrievalStatus.FileLost => NotFound(
                "This voice note was recorded on the ticket but could not be found in storage."),
            _ => NotFound("This ticket has no voice note."),
        };
    }

    /// <summary>
    /// Maps a CanAccessAttachmentAsync result to the right HTTP response:
    /// RecordNotFound is a genuine 404 (the ticket itself doesn't exist),
    /// while Forbidden is an authenticated caller who exists but isn't
    /// permitted — a 403 via ForbidOwnership, tagged so the frontend can
    /// tell it apart from an expired-token 403 and show an accurate
    /// message instead of "this file could not be found".
    /// </summary>
    private IActionResult AttachmentAccessError(AttachmentAccessResult access) => access switch
    {
        AttachmentAccessResult.RecordNotFound => NotFound(),
        AttachmentAccessResult.Forbidden => this.ForbidOwnership(),
        _ => NotFound(),
    };
}