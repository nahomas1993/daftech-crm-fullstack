using DaftechCrm.Api.Auth;
using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DaftechCrm.Api.Controllers;

public record TouchSessionRequest(SessionAccountType AccountType, Guid AccountId);
public record CloseSessionRequest(SessionAccountType AccountType, Guid AccountId);

[ApiController]
[Route("api/sessions")]
[Authorize(Policy = AuthorizationPolicies.AnyAuthenticated)]
public class SessionsController : ControllerBase
{
    private readonly ISessionService _sessions;
    public SessionsController(ISessionService sessions) => _sessions = sessions;

    /// <summary>Admin's Session Activity page — current online/offline status, last-seen, and most recent IP per account.</summary>
    [HttpGet("activity")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<IReadOnlyList<SessionActivityDto>>> GetActivity(CancellationToken ct) =>
        Ok(await _sessions.GetSessionActivityAsync(ct));

    [HttpGet("history")]
    [Authorize(Policy = AuthorizationPolicies.AdminOnly)]
    public async Task<ActionResult<IReadOnlyList<LoginSessionDto>>> GetHistory(
        [FromQuery] SessionAccountType accountType, [FromQuery] Guid accountId, CancellationToken ct) =>
        Ok(await _sessions.GetHistoryForAccountAsync(accountType, accountId, ct));

    /// <summary>
    /// Heartbeat — the frontend calls this periodically while the tab is
    /// active to keep OnlineStatus true and LastSeen current. Account
    /// identity is read from the caller's own access token, not the
    /// request body — otherwise any authenticated user could touch or
    /// close another account's session by passing a different AccountId.
    /// </summary>
    [HttpPost("touch")]
    public async Task<IActionResult> Touch(CancellationToken ct)
    {
        var (accountType, accountId) = CallerIdentity.Resolve(User);
        await _sessions.TouchAsync(accountType, accountId, ct);
        return NoContent();
    }

    [HttpPost("close")]
    public async Task<IActionResult> Close(CancellationToken ct)
    {
        var (accountType, accountId) = CallerIdentity.Resolve(User);
        await _sessions.CloseSessionAsync(accountType, accountId, ct);
        return NoContent();
    }
}
