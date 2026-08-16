using DaftechCrm.Api.Auth;
using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DaftechCrm.Api.Controllers;

[ApiController]
[Route("api/clients")]
[Authorize(Policy = AuthorizationPolicies.AnyEmployee)]
public class ClientsController : ControllerBase
{
    private readonly IClientService _clients;
    public ClientsController(IClientService clients) => _clients = clients;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ClientDto>>> GetAll(CancellationToken ct) => Ok(await _clients.GetAllAsync(ct));

    /// <summary>Paged client listing for the Clients table (query: page, pageSize).</summary>
    [HttpGet("paged")]
    public async Task<ActionResult<PagedResult<ClientDto>>> GetAllPaged([FromQuery] PaginationQuery query, CancellationToken ct) =>
        Ok(await _clients.GetAllPagedAsync(query, ct));

    /// <summary>
    /// A client may only fetch their own record (this is also what
    /// AuthService.restoreSession calls on the frontend after decoding its
    /// own JWT) — any employee may fetch any client, needed for the staff
    /// Client Detail page.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AnyAuthenticated)]
    public async Task<ActionResult<ClientDto>> GetById(Guid id, CancellationToken ct)
    {
        var (callerType, callerId) = CallerIdentity.Resolve(User);
        if (callerType == SessionAccountType.Client && callerId != id)
            return NotFound();

        var c = await _clients.GetByIdAsync(id, ct);
        return c is null ? NotFound() : Ok(c);
    }

    [HttpGet("pending")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<IReadOnlyList<ClientDto>>> GetPending(CancellationToken ct) => Ok(await _clients.GetPendingAsync(ct));

    /// <summary>Self-service signup — no account exists yet, so this is the one client-facing write endpoint that must stay anonymous.</summary>
    [HttpPost("signup")]
    [AllowAnonymous]
    public async Task<ActionResult<ClientDto>> Signup([FromBody] CreateClientSignupRequest request, CancellationToken ct)
    {
        var c = await _clients.SubmitSignupAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = c.Id }, c);
    }

    /// <summary>
    /// Admin registers a client directly — Approved and credentialed
    /// immediately, no separate approval step needed. The response's
    /// OneTimePassword is shown only once.
    /// </summary>
    [HttpPost("register")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<ClientRegisteredResult>> Register([FromBody] RegisterClientRequest request, CancellationToken ct)
    {
        var result = await _clients.RegisterAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Client.Id }, result);
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<ClientDto>> Approve(Guid id, CancellationToken ct) => Ok(await _clients.ApproveAsync(id, ct));

    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<ClientDto>> Reject(Guid id, [FromBody] RejectClientRequest request, CancellationToken ct) =>
        Ok(await _clients.RejectAsync(id, request, ct));

    /// <summary>Retries sending the credential email with a freshly regenerated one-time password (SRS v2.0 §4.3.1).</summary>
    [HttpPost("{id:guid}/resend-credential-email")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<ResendClientCredentialEmailResult>> ResendCredentialEmail(Guid id, CancellationToken ct)
    {
        try { return Ok(await _clients.ResendCredentialEmailAsync(id, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>Edits an existing client's profile fields. Account status/credentials go through approve/reject/resend instead.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<ClientDto>> Update(Guid id, [FromBody] UpdateClientRequest request, CancellationToken ct)
    {
        try { return Ok(await _clients.UpdateAsync(id, request, ct)); }
        catch (InvalidOperationException ex) { return NotFound(ex.Message); }
    }

    /// <summary>Soft-deletes the account — removes it from the Clients list and blocks login, but keeps agreements/tickets/trainings it's referenced by intact.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try { await _clients.DeleteAsync(id, ct); return NoContent(); }
        catch (InvalidOperationException ex) { return NotFound(ex.Message); }
    }
}

[ApiController]
[Route("api/agreements")]
[Authorize(Policy = AuthorizationPolicies.AnyAuthenticated)]
public class AgreementsController : ControllerBase
{
    private readonly IAgreementService _agreements;
    public AgreementsController(IAgreementService agreements) => _agreements = agreements;

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.AnyEmployee)]
    public async Task<ActionResult<IReadOnlyList<AgreementDto>>> GetAll(CancellationToken ct) => Ok(await _agreements.GetAllAsync(ct));

    /// <summary>Paged agreement listing for the Agreements table (query: page, pageSize).</summary>
    [HttpGet("paged")]
    [Authorize(Policy = AuthorizationPolicies.AnyEmployee)]
    public async Task<ActionResult<PagedResult<AgreementDto>>> GetAllPaged([FromQuery] PaginationQuery query, CancellationToken ct) =>
        Ok(await _agreements.GetAllPagedAsync(query, ct));

    /// <summary>A client may only list their own agreements; any employee may list any client's.</summary>
    [HttpGet("client/{clientId:guid}")]
    public async Task<ActionResult<IReadOnlyList<AgreementDto>>> GetForClient(Guid clientId, CancellationToken ct)
    {
        var (callerType, callerId) = CallerIdentity.Resolve(User);
        if (callerType == SessionAccountType.Client && callerId != clientId)
            return this.ForbidOwnership();

        return Ok(await _agreements.GetForClientAsync(clientId, ct));
    }

    [HttpGet("expiring-soon")]
    [Authorize(Policy = AuthorizationPolicies.AnyEmployee)]
    public async Task<ActionResult<IReadOnlyList<AgreementDto>>> GetExpiringSoon(CancellationToken ct) => Ok(await _agreements.GetExpiringSoonAsync(ct));

    /// <summary>
    /// Creates (signs) the support agreement. Rejected with 409 if the
    /// client has no completed training yet — training is mandatory and
    /// must finish before Daftech and the client sign the support
    /// agreement.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.AdminOrItSupport)]
    public async Task<ActionResult<AgreementDto>> Create([FromBody] CreateAgreementRequest request, CancellationToken ct)
    {
        try
        {
            var a = await _agreements.CreateAsync(request, ct);
            return Created($"/api/agreements/{a.Id}", a);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }

    /// <summary>
    /// Whether the client has at least one training with an End Date set —
    /// the precondition for signing an agreement. A client may only check
    /// their own status; any employee may check any client's.
    /// </summary>
    [HttpGet("client/{clientId:guid}/training-complete")]
    public async Task<ActionResult<bool>> ClientHasCompletedTraining(Guid clientId, CancellationToken ct)
    {
        var (callerType, callerId) = CallerIdentity.Resolve(User);
        if (callerType == SessionAccountType.Client && callerId != clientId)
            return this.ForbidOwnership();

        return Ok(await _agreements.ClientHasCompletedTrainingAsync(clientId, ct));
    }

    /// <summary>A client may only fetch their own agreement by id; any employee may fetch any agreement.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AgreementDto>> GetById(Guid id, CancellationToken ct)
    {
        var a = await _agreements.GetByIdAsync(id, ct);
        if (a is null) return NotFound();

        var (callerType, callerId) = CallerIdentity.Resolve(User);
        if (callerType == SessionAccountType.Client && callerId != a.ClientId)
            return NotFound();

        return Ok(a);
    }

    /// <summary>
    /// Uploads (or replaces) the scanned copy of the signed agreement.
    /// Accepts multipart/form-data with a single "file" field. Allowed
    /// types and max size are enforced server-side (see Storage options)
    /// regardless of what the browser's file picker allowed.
    /// </summary>
    [HttpPost("{id:guid}/scanned-file")]
    [Authorize(Policy = AuthorizationPolicies.AdminOrItSupport)]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<AgreementDto>> UploadScannedFile(Guid id, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest("No file was provided.");

        try
        {
            await using var stream = file.OpenReadStream();
            var dto = await _agreements.UploadScannedFileAsync(id, stream, file.FileName, file.ContentType, ct);
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

    /// <summary>Streams the agreement's scanned file back to the caller with its original content type. A client may only download their own agreement's file.</summary>
    [HttpGet("{id:guid}/scanned-file")]
    public async Task<IActionResult> DownloadScannedFile(Guid id, CancellationToken ct)
    {
        var (callerType, callerId) = CallerIdentity.Resolve(User);
        if (callerType == SessionAccountType.Client)
        {
            var owning = await _agreements.GetByIdAsync(id, ct);
            if (owning is null || owning.ClientId != callerId)
                return NotFound();
        }

        var file = await _agreements.DownloadScannedFileAsync(id, ct);
        return file is null ? NotFound() : File(file.Content, file.ContentType, file.OriginalFileName);
    }
}

/// <summary>
/// Client-scoped training records — deliberately NOT nested under
/// /api/agreements/{id}/trainings, because training happens (and must
/// finish) BEFORE any agreement exists for a client. See
/// AgreementService.CreateAsync for where a completed training becomes
/// the precondition for signing an agreement.
/// </summary>
[ApiController]
[Route("api/clients/{clientId:guid}/trainings")]
[Authorize(Policy = AuthorizationPolicies.AnyAuthenticated)]
public class ClientTrainingsController : ControllerBase
{
    private readonly IAgreementService _agreements;
    public ClientTrainingsController(IAgreementService agreements) => _agreements = agreements;

    /// <summary>A client may only list their own trainings; any employee may list any client's.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AgreementTrainingDto>>> GetForClient(Guid clientId, CancellationToken ct)
    {
        var (callerType, callerId) = CallerIdentity.Resolve(User);
        if (callerType == SessionAccountType.Client && callerId != clientId)
            return this.ForbidOwnership();

        return Ok(await _agreements.GetTrainingsForClientAsync(clientId, ct));
    }

    /// <summary>Adds a new, empty training row for the client — filled in and saved independently via PUT.</summary>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.AdminOrItSupport)]
    public async Task<ActionResult<AgreementTrainingDto>> AddTraining(Guid clientId, CancellationToken ct)
    {
        try { return Ok(await _agreements.AddTrainingAsync(clientId, ct)); }
        catch (InvalidOperationException ex) { return NotFound(ex.Message); }
    }

    /// <summary>Sets/updates one training row's description and timeline (start date + end date). EndDate stays editable even after being set, so the admin can push it out if training runs long.</summary>
    [HttpPut("{trainingId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOrItSupport)]
    public async Task<ActionResult<AgreementTrainingDto>> SaveTraining(Guid clientId, Guid trainingId, [FromBody] SaveAgreementTrainingRequest request, CancellationToken ct)
    {
        try { return Ok(await _agreements.SaveTrainingAsync(clientId, trainingId, request, ct)); }
        catch (Application.ValidationException ex) { return BadRequest(ex.Message); }
        catch (InvalidOperationException ex) { return NotFound(ex.Message); }
    }

    /// <summary>Deletes a training row.</summary>
    [HttpDelete("{trainingId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOrItSupport)]
    public async Task<IActionResult> DeleteTraining(Guid clientId, Guid trainingId, CancellationToken ct)
    {
        try { await _agreements.DeleteTrainingAsync(clientId, trainingId, ct); return NoContent(); }
        catch (InvalidOperationException ex) { return NotFound(ex.Message); }
    }

    /// <summary>
    /// Uploads (or replaces) the scanned copy of one training row's
    /// document. Accepts multipart/form-data with a single "file" field.
    /// </summary>
    [HttpPost("{trainingId:guid}/scan")]
    [Authorize(Policy = AuthorizationPolicies.AdminOrItSupport)]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<AgreementTrainingDto>> UploadTrainingScan(Guid clientId, Guid trainingId, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest("No file was provided.");

        try
        {
            await using var stream = file.OpenReadStream();
            var dto = await _agreements.UploadTrainingScanAsync(clientId, trainingId, stream, file.FileName, file.ContentType, ct);
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

    /// <summary>Streams a training row's scan back to the caller with its original content type. A client may only download their own training's scan.</summary>
    [HttpGet("{trainingId:guid}/scan")]
    public async Task<IActionResult> DownloadTrainingScan(Guid clientId, Guid trainingId, CancellationToken ct)
    {
        var (callerType, callerId) = CallerIdentity.Resolve(User);
        if (callerType == SessionAccountType.Client && callerId != clientId)
            return NotFound();

        var file = await _agreements.DownloadTrainingScanAsync(clientId, trainingId, ct);
        return file is null ? NotFound() : File(file.Content, file.ContentType, file.OriginalFileName);
    }
}

[ApiController]
[Route("api/maintenance")]
[Authorize(Policy = AuthorizationPolicies.AnyEmployee)]
public class MaintenanceController : ControllerBase
{
    private readonly IMaintenanceService _maintenance;
    public MaintenanceController(IMaintenanceService maintenance) => _maintenance = maintenance;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MaintenanceRecordDto>>> GetAll(CancellationToken ct) => Ok(await _maintenance.GetAllAsync(ct));

    /// <summary>Paged maintenance record listing for the Maintenance table (query: page, pageSize).</summary>
    [HttpGet("paged")]
    public async Task<ActionResult<PagedResult<MaintenanceRecordDto>>> GetAllPaged([FromQuery] PaginationQuery query, CancellationToken ct) =>
        Ok(await _maintenance.GetAllPagedAsync(query, ct));

    [HttpPost]
    public async Task<ActionResult<MaintenanceRecordDto>> Create([FromBody] CreateMaintenanceRecordRequest request, CancellationToken ct)
    {
        var r = await _maintenance.CreateAsync(request, ct);
        return Created($"/api/maintenance/{r.Id}", r);
    }
}

[ApiController]
[Route("api/time-logs")]
[Authorize(Policy = AuthorizationPolicies.AnyEmployee)]
public class TimeLogsController : ControllerBase
{
    private readonly ITimeLogService _timeLogs;
    public TimeLogsController(ITimeLogService timeLogs) => _timeLogs = timeLogs;

    /// <summary>
    /// Resolves the effective employeeId filter for a non-admin caller: a
    /// non-admin may only ever see their own logs, regardless of what (if
    /// anything) they passed in the query string — an omitted or
    /// someone-else's employeeId is silently narrowed to the caller's own
    /// id rather than rejected, since the frontend never intentionally
    /// omits it for a non-admin (see time-tracking.component.ts) and this
    /// keeps a stray/old request from a non-admin from ever exposing
    /// another employee's clock-in/out history.
    /// </summary>
    private Guid? EffectiveEmployeeFilter(Guid? requested)
    {
        if (User.IsInRole(nameof(EmployeeRole.Admin)))
            return requested;

        var (_, callerId) = CallerIdentity.Resolve(User);
        return callerId;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TimeLogDto>>> GetAll([FromQuery] Guid? employeeId, CancellationToken ct) =>
        Ok(await _timeLogs.GetAllAsync(EffectiveEmployeeFilter(employeeId), ct));

    /// <summary>Paged time log listing for the Time Tracking table (query: employeeId, page, pageSize).</summary>
    [HttpGet("paged")]
    public async Task<ActionResult<PagedResult<TimeLogDto>>> GetAllPaged([FromQuery] Guid? employeeId, [FromQuery] PaginationQuery query, CancellationToken ct) =>
        Ok(await _timeLogs.GetAllPagedAsync(EffectiveEmployeeFilter(employeeId), query, ct));

    /// <summary>A non-admin employee may only clock themselves in.</summary>
    [HttpPost("{employeeId:guid}/clock-in")]
    public async Task<IActionResult> ClockIn(Guid employeeId, CancellationToken ct)
    {
        if (!User.IsInRole(nameof(EmployeeRole.Admin)))
        {
            var (_, callerId) = CallerIdentity.Resolve(User);
            if (callerId != employeeId)
                return this.ForbidOwnership();
        }

        await _timeLogs.ClockInAsync(employeeId, ct);
        return NoContent();
    }

    /// <summary>A non-admin employee may only clock themselves out.</summary>
    [HttpPost("{employeeId:guid}/clock-out")]
    public async Task<IActionResult> ClockOut(Guid employeeId, CancellationToken ct)
    {
        if (!User.IsInRole(nameof(EmployeeRole.Admin)))
        {
            var (_, callerId) = CallerIdentity.Resolve(User);
            if (callerId != employeeId)
                return this.ForbidOwnership();
        }

        await _timeLogs.ClockOutAsync(employeeId, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/notifications")]
[Authorize(Policy = AuthorizationPolicies.AnyAuthenticated)]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notifications;
    public NotificationsController(INotificationService notifications) => _notifications = notifications;

    /// <summary>
    /// Verifies the caller may see notifications addressed to
    /// (recipientType, recipientId): Employee/Client notifications must be
    /// addressed to the caller's own account id; Admin (broadcast, e.g.
    /// "ALL_ADMIN") notifications are visible to any Admin. Without this,
    /// any authenticated user could read or mark-read another account's
    /// notifications just by passing a different recipientId.
    /// </summary>
    private bool CallerOwnsRecipient(NotificationRecipientType recipientType, string recipientId)
    {
        var (callerType, callerId) = CallerIdentity.Resolve(User);

        if (recipientType == NotificationRecipientType.Admin)
            return User.IsInRole(nameof(EmployeeRole.Admin));

        if (recipientType == NotificationRecipientType.Employee)
            return callerType == SessionAccountType.Employee && callerId.ToString() == recipientId;

        if (recipientType == NotificationRecipientType.Client)
            return callerType == SessionAccountType.Client && callerId.ToString() == recipientId;

        return false;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NotificationDto>>> GetForRecipient(
        [FromQuery] NotificationRecipientType recipientType, [FromQuery] string recipientId, CancellationToken ct)
    {
        if (!CallerOwnsRecipient(recipientType, recipientId))
            return this.ForbidOwnership();

        return Ok(await _notifications.GetForRecipientAsync(recipientType, recipientId, ct));
    }

    /// <summary>A caller may only mark their own notification read (Admins may mark any Admin-broadcast notification read).</summary>
    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        var (callerType, callerId) = CallerIdentity.Resolve(User);
        var isAdmin = User.IsInRole(nameof(EmployeeRole.Admin));

        var ok = await _notifications.MarkReadAsync(id, callerType, callerId, isAdmin, ct);
        return ok ? NoContent() : NotFound();
    }

    [HttpPost("mark-all-read")]
    public async Task<IActionResult> MarkAllRead([FromQuery] NotificationRecipientType recipientType, [FromQuery] string recipientId, CancellationToken ct)
    {
        if (!CallerOwnsRecipient(recipientType, recipientId))
            return this.ForbidOwnership();

        await _notifications.MarkAllReadAsync(recipientType, recipientId, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/reports")]
[Authorize(Policy = AuthorizationPolicies.AnyEmployee)]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reports;
    public ReportsController(IReportService reports) => _reports = reports;

    /// <summary>Bar chart (per-employee) and donut chart (overall) data for on-time vs late ticket resolution.</summary>
    [HttpGet("on-time-resolution")]
    public async Task<ActionResult<OnTimeReportDto>> GetOnTimeResolution(CancellationToken ct) =>
        Ok(await _reports.GetOnTimeResolutionReportAsync(ct));

    /// <summary>Live system-wide snapshot (ticket status breakdown + headline counts) for the admin "Overall Operations" pie chart.</summary>
    [HttpGet("operations-overview")]
    public async Task<ActionResult<OperationsOverviewDto>> GetOperationsOverview(CancellationToken ct) =>
        Ok(await _reports.GetOperationsOverviewAsync(ct));

    /// <summary>
    /// Written/graphical performance metrics for one employee. Pass
    /// includeAiNarrative=true to also request the optional AI summary —
    /// omit or set false to skip the AI call entirely (e.g. for a fast
    /// numbers-only view).
    /// </summary>
    [HttpGet("employee-performance/{employeeId:guid}")]
    public async Task<ActionResult<EmployeePerformanceReportDto>> GetEmployeePerformance(
        Guid employeeId, [FromQuery] bool includeAiNarrative, CancellationToken ct)
    {
        try { return Ok(await _reports.GetEmployeePerformanceReportAsync(employeeId, includeAiNarrative, ct)); }
        catch (InvalidOperationException ex) { return NotFound(ex.Message); }
    }

    /// <summary>
    /// AI narrative summary for any report table already rendered on the
    /// Reports page (staff or client portal). The frontend sends the same
    /// columns/rows it's already showing on screen — this never recomputes
    /// numbers, it only narrates what's given. Always best-effort: a
    /// non-2xx or Available=false response should never block the table
    /// itself from displaying.
    /// </summary>
    [HttpPost("summarize")]
    public async Task<ActionResult<AiPerformanceSummaryResult>> Summarize([FromBody] TabularReportData data, CancellationToken ct)
    {
        if (data.Rows.Count > 5000)
            return BadRequest("Report is too large to summarize in one request.");

        return Ok(await _reports.SummarizeTabularReportAsync(data, ct));
    }
}

[ApiController]
[Route("api/system-configuration")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class SystemConfigurationController : ControllerBase
{
    private readonly ISystemConfigurationService _config;
    public SystemConfigurationController(ISystemConfigurationService config) => _config = config;

    /// <summary>All admin-configurable settings, grouped by Category, for the Settings → Configuration page.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SystemSettingDto>>> GetAll(CancellationToken ct) => Ok(await _config.GetAllAsync(ct));

    /// <summary>Saves one or more settings at once. Admin-only; the caller's name is recorded against each changed value.</summary>
    [HttpPut]
    public async Task<ActionResult<IReadOnlyList<SystemSettingDto>>> Update([FromBody] UpdateSystemSettingsRequest request, CancellationToken ct)
    {
        var callerName = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.UniqueName)?.Value ?? "Admin";
        try { return Ok(await _config.UpdateAsync(request, callerName, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }
}

[ApiController]
[Route("api/satisfaction-surveys")]
[Authorize(Policy = AuthorizationPolicies.AnyAuthenticated)]
public class SatisfactionSurveysController : ControllerBase
{
    private readonly ISatisfactionSurveyService _surveys;
    public SatisfactionSurveysController(ISatisfactionSurveyService surveys) => _surveys = surveys;

    [HttpGet]
    [Authorize(Policy = AuthorizationPolicies.AnyEmployee)]
    public async Task<ActionResult<IReadOnlyList<SatisfactionSurveyDto>>> GetAll(CancellationToken ct) => Ok(await _surveys.GetAllAsync(ct));

    /// <summary>Paged survey listing for the Satisfaction Surveys table (query: page, pageSize).</summary>
    [HttpGet("paged")]
    [Authorize(Policy = AuthorizationPolicies.AnyEmployee)]
    public async Task<ActionResult<PagedResult<SatisfactionSurveyDto>>> GetAllPaged([FromQuery] PaginationQuery query, CancellationToken ct) =>
        Ok(await _surveys.GetAllPagedAsync(query, ct));

    /// <summary>A client may only view their own ticket's survey; any employee may view any ticket's survey.</summary>
    [HttpGet("ticket/{ticketId:guid}")]
    public async Task<ActionResult<SatisfactionSurveyDto>> GetForTicket(Guid ticketId, CancellationToken ct)
    {
        var survey = await _surveys.GetForTicketAsync(ticketId, ct);
        if (survey is null) return NotFound();

        var (callerType, callerId) = CallerIdentity.Resolve(User);
        if (callerType == SessionAccountType.Client && callerId != survey.ClientId)
            return NotFound();

        return Ok(survey);
    }

    /// <summary>
    /// The client identity always comes from the caller's own JWT (see
    /// CallerIdentity), never from request.ClientId — same reasoning as
    /// TicketsController.Submit: without this override, any authenticated
    /// client could submit a survey response under a different client's
    /// identity by editing the request body.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.AnyClient)]
    public async Task<ActionResult<SatisfactionSurveyDto>> Submit([FromBody] SubmitSatisfactionSurveyRequest request, CancellationToken ct)
    {
        var (_, callerId) = CallerIdentity.Resolve(User);
        var effectiveRequest = request with { ClientId = callerId };

        try { return Ok(await _surveys.SubmitAsync(effectiveRequest, ct)); }
        catch (ArgumentOutOfRangeException ex) { return BadRequest(ex.Message); }
    }
}

[ApiController]
[Route("api/locations")]
public class LocationsController : ControllerBase
{
    private readonly ILocationService _locations;
    public LocationsController(ILocationService locations) => _locations = locations;

    /// <summary>All admin-managed dropdown/checklist options (Region/City/Woreda/Specialization/CustomRole). Public (no [Authorize]) — the client self-signup portal needs Region/City/Woreda while unauthenticated.</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<LocationOptionsDto>> GetAll(CancellationToken ct) => Ok(await _locations.GetAllAsync(ct));

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<LocationEntryDto>> Create([FromBody] CreateLocationEntryRequest request, CancellationToken ct)
    {
        try { return Ok(await _locations.CreateAsync(request, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<LocationEntryDto>> Update(Guid id, [FromBody] UpdateLocationEntryRequest request, CancellationToken ct)
    {
        try { return Ok(await _locations.UpdateAsync(id, request, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try { await _locations.DeleteAsync(id, ct); return NoContent(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }
}

[ApiController]
[Route("api/failure-types")]
public class FailureTypesController : ControllerBase
{
    private readonly IFailureTypeService _failureTypes;
    public FailureTypesController(IFailureTypeService failureTypes) => _failureTypes = failureTypes;

    /// <summary>Public — the client portal's Submit Issue form needs this list to populate the dropdown.</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<FailureTypeDto>>> GetAll(CancellationToken ct) => Ok(await _failureTypes.GetAllAsync(ct));

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<FailureTypeDto>> Create([FromBody] CreateFailureTypeRequest request, CancellationToken ct)
    {
        try { return Ok(await _failureTypes.CreateAsync(request, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<FailureTypeDto>> Update(Guid id, [FromBody] UpdateFailureTypeRequest request, CancellationToken ct)
    {
        try { return Ok(await _failureTypes.UpdateAsync(id, request, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try { await _failureTypes.DeleteAsync(id, ct); return NoContent(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }
}
