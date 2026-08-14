using DaftechCrm.Domain.Enums;

namespace DaftechCrm.Domain.Entities;

/// <summary>
/// A server-side record of an issued refresh token. We never store the
/// raw token — only a SHA-256 hash of it — so a database leak alone can't
/// be used to mint new access tokens. Supports rotation: each refresh
/// consumes the old token (sets RevokedAt + ReplacedByTokenHash) and
/// issues a new one, so a stolen-and-reused token is detectable.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public SessionAccountType AccountType { get; set; }
    public Guid AccountId { get; set; }

    /// <summary>SHA-256 hash (hex) of the raw refresh token. The raw value is returned to the client once and never persisted.</summary>
    public string TokenHash { get; set; } = default!;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }

    public string CreatedByIp { get; set; } = default!;

    public DateTimeOffset? RevokedAt { get; set; }
    public string? RevokedByIp { get; set; }

    /// <summary>If this token was rotated, the hash of the token that replaced it — lets us detect reuse of a stale token.</summary>
    public string? ReplacedByTokenHash { get; set; }

    public bool IsActive => RevokedAt is null && DateTimeOffset.UtcNow < ExpiresAt;
}
