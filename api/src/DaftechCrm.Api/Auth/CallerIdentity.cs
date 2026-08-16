using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using DaftechCrm.Domain.Enums;
using DaftechCrm.Infrastructure.Auth;
using Microsoft.AspNetCore.Mvc;

namespace DaftechCrm.Api.Auth;

/// <summary>
/// Resolves the calling account's type and ID from the validated JWT
/// claims on the current request — the source of truth for "who is
/// calling", instead of trusting a client-supplied AccountId in the
/// request body (which any authenticated caller could forge to act as a
/// different account).
/// </summary>
public static class CallerIdentity
{
    /// <summary>
    /// Header set on a 403 to mark it as a genuine, authenticated-caller
    /// ownership/permission violation — e.g. "you're not the technician
    /// assigned to this ticket" — as opposed to ASP.NET Core's own
    /// auth-pipeline 403, which can occur when an expired/invalid JWT
    /// fails OnAuthenticationFailed and the subsequent role/claim check
    /// has no identity to check against at all. The frontend's auth
    /// interceptor uses this to skip a pointless silent refresh-and-retry
    /// for the former case, since a fresh token wouldn't change the
    /// outcome — the caller's identity was valid all along, they're just
    /// not allowed to touch this particular resource.
    /// </summary>
    public const string OwnershipForbiddenHeader = "X-Forbidden-Reason";
    public const string OwnershipForbiddenValue = "not-owner";

    public static (SessionAccountType AccountType, Guid AccountId) Resolve(ClaimsPrincipal user)
    {
        var accountTypeClaim = user.FindFirst(DaftechClaimTypes.AccountType)?.Value
            ?? throw new InvalidOperationException("Token is missing the account type claim.");

        var subClaim = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new InvalidOperationException("Token is missing the subject claim.");

        if (!Enum.TryParse<SessionAccountType>(accountTypeClaim, out var accountType))
            throw new InvalidOperationException("Token has an unrecognized account type claim.");

        if (!Guid.TryParse(subClaim, out var accountId))
            throw new InvalidOperationException("Token has an unrecognized subject claim.");

        return (accountType, accountId);
    }
}

public static class ControllerBaseAuthExtensions
{
    /// <summary>
    /// Returns a 403 tagged as a genuine ownership/permission violation
    /// (see CallerIdentity.OwnershipForbiddenHeader) rather than a bare
    /// Forbid(). Use this — not Forbid() directly — for every "caller is
    /// authenticated but doesn't own/isn't permitted to touch this
    /// specific resource" check, so the frontend can distinguish it from
    /// an expired-token 403.
    /// </summary>
    public static IActionResult ForbidOwnership(this ControllerBase controller)
    {
        controller.Response.Headers[CallerIdentity.OwnershipForbiddenHeader] = CallerIdentity.OwnershipForbiddenValue;
        return controller.Forbid();
    }
}
