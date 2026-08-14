namespace DaftechCrm.Application.Options;

/// <summary>
/// Bound from appsettings.json ("Jwt" section) / user-secrets in
/// development. SigningKey must be at least 32 bytes of random data —
/// generate with e.g. `openssl rand -base64 48` and never commit a real
/// value to source control.
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Symmetric signing key (HMAC-SHA256). Must be kept secret and be at least 32 bytes when UTF8-encoded.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public string Issuer { get; set; } = "DaftechCrm";
    public string Audience { get; set; } = "DaftechCrm.Client";

    /// <summary>How long an access token is valid for. Kept short since it can't be revoked before expiry.</summary>
    public int AccessTokenMinutes { get; set; } = 15;

    /// <summary>How long a refresh token is valid for. Refresh tokens are stored server-side and can be revoked.</summary>
    public int RefreshTokenDays { get; set; } = 14;
}
