using DaftechCrm.Api.Auth;
using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Interfaces;
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

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.AnyEmployee)]
    public async Task<ActionResult<IReadOnlyList<TicketDto>>> GetAll(
        CancellationToken ct) =>
        Ok(await _tickets.GetAllAsync(ct));

    /// <summary>
    /// Paged ticket listing for the Tickets table
    /// (query: page, pageSize).
    /// </summary>
    [HttpGet("paged")]
    [Authorize(Policy = AuthorizationPolicies.AnyEmployee)]
    public async Task<ActionResult<PagedResult<TicketDto>>> GetAllPaged(
        [FromQuery] PaginationQuery query,
        CancellationToken ct) =>
        Ok(await _tickets.GetAllPagedAsync(query, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TicketDto>> GetById(
        Guid id,
        CancellationToken ct)
    {
        var ticket =
            await _tickets.GetByIdAsync(id, ct);

        return ticket is null
            ? NotFound()
            : Ok(ticket);
    }

    [HttpGet("client/{clientId:guid}")]
    public async Task<ActionResult<IReadOnlyList<TicketDto>>> GetForClient(
        Guid clientId,
        CancellationToken ct) =>
        Ok(await _tickets.GetForClientAsync(
            clientId,
            ct));

    [HttpGet("employee/{employeeId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AnyEmployee)]
    public async Task<ActionResult<IReadOnlyList<TicketDto>>> GetForEmployee(
        Guid employeeId,
        CancellationToken ct) =>
        Ok(await _tickets.GetForEmployeeAsync(
            employeeId,
            ct));

    [HttpGet("client/{clientId:guid}/awaiting-confirmation")]
    public async Task<ActionResult<IReadOnlyList<TicketDto>>>
        GetAwaitingConfirmation(
            Guid clientId,
            CancellationToken ct) =>
        Ok(await _tickets.GetAwaitingConfirmationForClientAsync(
            clientId,
            ct));

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
    /// on submission.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.AnyClient)]
    public async Task<ActionResult<TicketDto>> Submit(
        [FromBody] SubmitTicketRequest request,
        CancellationToken ct)
    {
        try
        {
            var ticket =
                await _tickets.SubmitFromClientAsync(
                    request,
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
        try
        {
            return Ok(
                await _tickets.UpdateStatusAsync(
                    id,
                    request,
                    ct));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(ex.Message);
        }
    }

    /// <summary>
    /// Client confirms the fix and rates 1-5 stars.
    /// Score = stars * 20.
    /// </summary>
    [HttpPost("{id:guid}/confirm")]
    [Authorize(Policy = AuthorizationPolicies.AnyClient)]
    public async Task<ActionResult<TicketDto>> Confirm(
        Guid id,
        [FromBody] ClientConfirmationRequest request,
        CancellationToken ct)
    {
        try
        {
            return Ok(
                await _tickets.ConfirmResolutionAsync(
                    id,
                    request,
                    ct));
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

        if (!await _tickets.CanAccessAttachmentAsync(
            id,
            callerType,
            callerId,
            ct))
        {
            return NotFound();
        }

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

        if (!await _tickets.CanAccessAttachmentAsync(
            id,
            callerType,
            callerId,
            ct))
        {
            return NotFound();
        }

        var file =
            await _tickets.DownloadAttachmentAsync(
                id,
                ct);

        return file is null
            ? NotFound()
            : File(
                file.Content,
                file.ContentType,
                file.OriginalFileName);
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

        if (!await _tickets.CanAccessAttachmentAsync(
            id,
            callerType,
            callerId,
            ct))
        {
            return NotFound();
        }

        var file =
            await _tickets.DownloadVoiceNoteAsync(
                id,
                ct);

        return file is null
            ? NotFound()
            : File(
                file.Content,
                file.ContentType,
                file.OriginalFileName);
    }
}