using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using DaftechCrm.Domain.Enums;
using DaftechCrm.Infrastructure.Auth;

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
