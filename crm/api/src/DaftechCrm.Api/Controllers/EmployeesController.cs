using DaftechCrm.Api.Auth;
using DaftechCrm.Api.Extensions;
using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DaftechCrm.Api.Controllers;

[ApiController]
[Route("api/employees")]
[Authorize(Policy = AuthorizationPolicies.AnyEmployee)]
public class EmployeesController : ControllerBase
{
    private readonly IEmployeeService _employees;
    public EmployeesController(IEmployeeService employees) => _employees = employees;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EmployeeDto>>> GetAll(CancellationToken ct) => Ok(await _employees.GetAllAsync(ct));

    /// <summary>Paged employee listing for the Employees table (query: page, pageSize).</summary>
    [HttpGet("paged")]
    public async Task<ActionResult<PagedResult<EmployeeDto>>> GetAllPaged([FromQuery] PaginationQuery query, CancellationToken ct) =>
        Ok(await _employees.GetAllPagedAsync(query, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EmployeeDto>> GetById(Guid id, CancellationToken ct)
    {
        var e = await _employees.GetByIdAsync(id, ct);
        return e is null ? NotFound() : Ok(e);
    }

    /// <summary>
    /// Admin registers a new staff account. The response includes the
    /// system-generated username and a one-time password — this is the
    /// ONLY time the plaintext one-time password is ever available. The
    /// Admin must relay it to the employee immediately; it cannot be
    /// retrieved again afterward.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<EmployeeRegisteredResult>> Register([FromBody] CreateEmployeeRequest request, CancellationToken ct)
    {
        var result = await _employees.RegisterAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Employee.Id }, result);
    }

    /// <summary>Disables the account (offboarding) — revokes all device sessions and blocks future logins immediately.</summary>
    [HttpPost("{id:guid}/disable")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<EmployeeDto>> Disable(Guid id, [FromBody] DisableEmployeeRequest request, CancellationToken ct)
    {
        try { return Ok(await _employees.DisableAsync(id, request, ct)); }
        catch (InvalidOperationException ex) { return NotFound(ex.Message); }
    }

    [HttpPost("{id:guid}/enable")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<EmployeeDto>> Enable(Guid id, CancellationToken ct)
    {
        try { return Ok(await _employees.EnableAsync(id, ct)); }
        catch (InvalidOperationException ex) { return NotFound(ex.Message); }
    }

    /// <summary>Edits an existing employee's profile fields (name/email/phone/specialization). Responsibilities, IP allow-list, and enable/disable each have their own endpoints.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<EmployeeDto>> Update(Guid id, [FromBody] UpdateEmployeeRequest request, CancellationToken ct)
    {
        try { return Ok(await _employees.UpdateAsync(id, request, ct)); }
        catch (InvalidOperationException ex) { return NotFound(ex.Message); }
    }

    /// <summary>Replaces the employee's full set of responsibilities (Admin/EmployeeTechnician/Trainer) — an Admin can add, remove, or change these at any time. Not merged with the previous set — send the complete new list.</summary>
    [HttpPut("{id:guid}/responsibilities")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<EmployeeDto>> SetResponsibilities(Guid id, [FromBody] SetEmployeeResponsibilitiesRequest request, CancellationToken ct)
    {
        try { return Ok(await _employees.SetResponsibilitiesAsync(id, request, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    /// <summary>Soft-deletes the account — removes it from the Employees list and blocks login, but keeps tickets/time logs/etc. it's referenced by intact.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try { await _employees.DeleteAsync(id, ct); return NoContent(); }
        catch (InvalidOperationException ex) { return NotFound(ex.Message); }
    }

    [HttpPost("{id:guid}/allowed-ips")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<EmployeeDto>> AddAllowedIp(Guid id, [FromBody] AddAllowedIpRequest request, CancellationToken ct) =>
        Ok(await _employees.AddAllowedIpAsync(id, request, ct));

    [HttpDelete("{id:guid}/allowed-ips/{ip}")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<EmployeeDto>> RemoveAllowedIp(Guid id, string ip, CancellationToken ct) =>
        Ok(await _employees.RemoveAllowedIpAsync(id, ip, ct));

    [HttpGet("{id:guid}/devices")]
    [Authorize(Policy = AuthorizationPolicies.AdminOrItSupport)]
    public async Task<ActionResult<IReadOnlyList<DeviceSessionDto>>> GetDevices(Guid id, CancellationToken ct) =>
        Ok(await _employees.GetDevicesAsync(id, ct));

    [HttpPost("devices/{deviceSessionId:guid}/revoke")]
    [Authorize(Policy = AuthorizationPolicies.AdminOrItSupport)]
    public async Task<IActionResult> RevokeDevice(Guid deviceSessionId, CancellationToken ct)
    {
        await _employees.RevokeDeviceAsync(deviceSessionId, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/login-history")]
    [Authorize(Policy = AuthorizationPolicies.AdminOrItSupport)]
    public async Task<ActionResult<IReadOnlyList<LoginRecordDto>>> GetLoginHistory(Guid id, CancellationToken ct) =>
        Ok(await _employees.GetLoginHistoryAsync(id, ct));

    /// <summary>Retries sending the credential email with a freshly regenerated one-time password (SRS v2.0 §4.3.1).</summary>
    [HttpPost("{id:guid}/resend-credential-email")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<ResendCredentialEmailResult>> ResendCredentialEmail(Guid id, CancellationToken ct)
    {
        try { return Ok(await _employees.ResendCredentialEmailAsync(id, ct)); }
        catch (InvalidOperationException ex) { return NotFound(ex.Message); }
    }
}

/// <summary>
/// Login, token refresh, and password-change endpoints. These are the only
/// endpoints in the API that allow anonymous access — everything else
/// requires a valid access token. Rate-limited more strictly than the rest
/// of the API (see RateLimitingExtensions.AuthPolicy) to slow down
/// credential-stuffing and brute-force attempts.
/// </summary>
[ApiController]
[Route("api/auth")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitingExtensions.AuthPolicy)]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly Application.Interfaces.ICurrentRequestContext _requestContext;
    public AuthController(IAuthService auth, Application.Interfaces.ICurrentRequestContext requestContext)
    {
        _auth = auth;
        _requestContext = requestContext;
    }

    /// <summary>
    /// Unified login — the single entry point used by the one login page
    /// for Admins, Employees, and Clients alike. The server determines the
    /// account type itself by which table the username belongs to; the
    /// frontend never sends or chooses an account type. See
    /// AuthService.LoginAsync for the lookup logic; role/permission
    /// enforcement itself still happens purely via the JWT claims issued
    /// here, per AuthorizationPolicies.
    /// </summary>
    [HttpPost("login")]
    public async Task<ActionResult<UnifiedLoginResult>> Login([FromBody] UnifiedLoginRequest request, CancellationToken ct) =>
        Ok(await _auth.LoginAsync(request, ct));

    /// <summary>
    /// Employee login. The server resolves the caller's IP address itself
    /// (see HttpCurrentRequestContext) — it is not supplied by the client —
    /// and records it on every attempt, successful or blocked. The response's
    /// MustChangePassword flag tells the frontend to route straight to the
    /// change-password screen before anything else; Tokens is null until
    /// the password has actually been changed.
    ///
    /// Kept alongside the unified /login endpoint above for any direct
    /// API callers that still target the employee-specific shape.
    /// </summary>
    [HttpPost("employee-login")]
    public async Task<ActionResult<EmployeeLoginResult>> LoginEmployee([FromBody] EmployeeLoginRequest request, CancellationToken ct) =>
        Ok(await _auth.LoginEmployeeAsync(request, ct));

    [HttpPost("employee/{employeeId:guid}/change-password")]
    public async Task<IActionResult> ChangeEmployeePassword(Guid employeeId, [FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        try
        {
            await _auth.ChangeEmployeePasswordAsync(employeeId, request, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("client-login")]
    public async Task<ActionResult<ClientLoginResult>> LoginClient([FromBody] ClientLoginRequest request, CancellationToken ct) =>
        Ok(await _auth.LoginClientAsync(request, ct));

    [HttpPost("client/{clientId:guid}/change-password")]
    public async Task<IActionResult> ChangeClientPassword(Guid clientId, [FromBody] ClientChangePasswordRequest request, CancellationToken ct)
    {
        try
        {
            await _auth.ChangeClientPasswordAsync(clientId, request, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Exchanges a refresh token for a new access/refresh pair. The old
    /// refresh token is rotated (revoked and replaced) — reusing it again
    /// after this call will fail and revoke all sessions for the account
    /// as a precaution against token theft.
    /// </summary>
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthTokenResult>> Refresh([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        try
        {
            var ip = _requestContext.ResolveClientIpAddress();
            return Ok(await _auth.RefreshAsync(request, ip, ct));
        }
        catch (InvalidOperationException ex)
        {
            return Unauthorized(new { error = ex.Message });
        }
    }

    /// <summary>Logs out on one device by revoking its refresh token. Safe to call even if the token is already gone.</summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] RevokeTokenRequest request, CancellationToken ct)
    {
        var ip = _requestContext.ResolveClientIpAddress();
        await _auth.RevokeRefreshTokenAsync(request, ip, ct);
        return NoContent();
    }

    /// <summary>
    /// "Forgot password" — there's no emailed reset link in this system, so
    /// this just queues the request for an Admin to review (see
    /// PasswordResetController). Always returns the same generic message,
    /// whether or not the username matched a real account, so this
    /// anonymous endpoint can't be used to enumerate valid usernames.
    /// </summary>
    [HttpPost("forgot-password")]
    public async Task<ActionResult<PasswordResetRequestSubmittedResult>> ForgotPassword(
        [FromBody] SubmitPasswordResetRequest request, [FromServices] IPasswordResetService resetService, CancellationToken ct)
    {
        var ip = _requestContext.ResolveClientIpAddress();
        return Ok(await resetService.SubmitAsync(request, ip, ct));
    }
}

/// <summary>
/// Admin's "Password Reset Requests" queue — reviews forgot-password
/// requests submitted anonymously from either login screen and either
/// issues a fresh one-time password (emailed, same as onboarding) or
/// dismisses the request.
/// </summary>
[ApiController]
[Route("api/password-reset-requests")]
[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class PasswordResetController : ControllerBase
{
    private readonly IPasswordResetService _resets;
    public PasswordResetController(IPasswordResetService resets) => _resets = resets;

    [HttpGet("pending")]
    public async Task<ActionResult<IReadOnlyList<PasswordResetRequestDto>>> GetPending(CancellationToken ct) =>
        Ok(await _resets.GetPendingAsync(ct));

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PasswordResetRequestDto>>> GetAll(CancellationToken ct) =>
        Ok(await _resets.GetAllAsync(ct));

    /// <summary>Issues a fresh one-time password and emails it to the account on file. The response's OneTimePassword is shown only once — same rule as registering a new hire.</summary>
    [HttpPost("{id:guid}/issue-otp")]
    public async Task<ActionResult<PasswordResetOtpIssuedResult>> IssueOtp(Guid id, CancellationToken ct)
    {
        var callerName = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.UniqueName)?.Value ?? "Admin";
        try { return Ok(await _resets.IssueOtpAsync(id, callerName, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpPost("{id:guid}/dismiss")]
    public async Task<ActionResult<PasswordResetRequestDto>> Dismiss(Guid id, [FromBody] DismissPasswordResetRequest request, CancellationToken ct)
    {
        var callerName = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.UniqueName)?.Value ?? "Admin";
        try { return Ok(await _resets.DismissAsync(id, callerName, request, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }
}
