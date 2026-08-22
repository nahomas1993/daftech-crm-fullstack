using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Application.Options;
using DaftechCrm.Domain.Entities;
using DaftechCrm.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DaftechCrm.Infrastructure.Auth;

/// <summary>
/// Claim type holding the account kind (Employee/Client) so authorization
/// policies can tell the two apart — see AuthorizationPolicies.
/// </summary>
public static class DaftechClaimTypes
{
    public const string AccountType = "daftech_account_type";
}

public class TokenService : ITokenService
{
    private readonly IAppDbContext _db;
    private readonly JwtOptions _options;
    private readonly ILogger<TokenService> _logger;

    public TokenService(IAppDbContext db, IOptions<JwtOptions> options, ILogger<TokenService> logger)
    {
        _db = db;
        _options = options.Value;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_options.SigningKey) || Encoding.UTF8.GetByteCount(_options.SigningKey) < 32)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey is missing or too short. Set a random value of at least 32 bytes via " +
                "`dotnet user-secrets set \"Jwt:SigningKey\" \"...\"` locally, or the Jwt__SigningKey " +
                "environment variable in production. Generate one with: openssl rand -base64 48");
        }
    }

    public async Task<IssuedTokenPair> IssueTokenPairAsync(TokenSubject subject, string ipAddress, CancellationToken ct = default)
    {
        var accessToken = CreateAccessToken(subject, out var expiresAt);
        var (rawRefreshToken, refreshTokenHash) = GenerateRefreshToken();

        _db.Add(new RefreshToken
        {
            AccountType = subject.AccountType,
            AccountId = subject.AccountId,
            TokenHash = refreshTokenHash,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(_options.RefreshTokenDays),
            CreatedByIp = ipAddress,
        });
        await _db.SaveChangesAsync(ct);

        return new IssuedTokenPair(accessToken, rawRefreshToken, expiresAt);
    }

    public async Task<IssuedTokenPair> RefreshAsync(string rawRefreshToken, string ipAddress, CancellationToken ct = default)
    {
        var hash = HashToken(rawRefreshToken);
        var existing = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (existing is null)
            throw new InvalidOperationException("Invalid refresh token.");

        if (existing.RevokedAt is not null)
        {
            // Reuse of an already-rotated/revoked token is a strong signal of token
            // theft — as a precaution, revoke every other active token for this
            // account so a stolen token can't keep minting new sessions.
            _logger.LogWarning(
                "Refresh token reuse detected for {AccountType} {AccountId} from {Ip} — revoking all active sessions for this account.",
                existing.AccountType, existing.AccountId, ipAddress);

            var others = await _db.RefreshTokens
                .Where(t => t.AccountType == existing.AccountType && t.AccountId == existing.AccountId && t.RevokedAt == null)
                .ToListAsync(ct);
            foreach (var t in others)
            {
                t.RevokedAt = DateTimeOffset.UtcNow;
                t.RevokedByIp = ipAddress;
                _db.Update(t);
            }
            await _db.SaveChangesAsync(ct);

            throw new InvalidOperationException("Refresh token has already been used. All sessions for this account have been revoked as a precaution.");
        }

        if (existing.ExpiresAt <= DateTimeOffset.UtcNow)
            throw new InvalidOperationException("Refresh token has expired.");

        var subject = await ResolveSubjectAsync(existing.AccountType, existing.AccountId, ct)
            ?? throw new InvalidOperationException("Account no longer exists.");

        var accessToken = CreateAccessToken(subject, out var expiresAt);
        var (rawNewRefreshToken, newHash) = GenerateRefreshToken();

        existing.RevokedAt = DateTimeOffset.UtcNow;
        existing.RevokedByIp = ipAddress;
        existing.ReplacedByTokenHash = newHash;
        _db.Update(existing);

        _db.Add(new RefreshToken
        {
            AccountType = subject.AccountType,
            AccountId = subject.AccountId,
            TokenHash = newHash,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(_options.RefreshTokenDays),
            CreatedByIp = ipAddress,
        });

        await _db.SaveChangesAsync(ct);

        return new IssuedTokenPair(accessToken, rawNewRefreshToken, expiresAt);
    }

    public async Task RevokeAsync(string rawRefreshToken, string ipAddress, CancellationToken ct = default)
    {
        var hash = HashToken(rawRefreshToken);
        var existing = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        // Revoking an unknown or already-revoked token is a no-op, not an
        // error — logout should never fail just because the token was
        // already gone (e.g. double-click logout, or it already expired).
        if (existing is null || existing.RevokedAt is not null)
            return;

        existing.RevokedAt = DateTimeOffset.UtcNow;
        existing.RevokedByIp = ipAddress;
        _db.Update(existing);
        await _db.SaveChangesAsync(ct);
    }

    private async Task<TokenSubject?> ResolveSubjectAsync(SessionAccountType accountType, Guid accountId, CancellationToken ct)
    {
        if (accountType == SessionAccountType.Employee)
        {
            var employee = await _db.Employees.FirstOrDefaultAsync(e => e.Id == accountId, ct);
            if (employee is null || employee.AccountStatus == EmployeeAccountStatus.Disabled)
                return null;
            return new TokenSubject(SessionAccountType.Employee, employee.Id, employee.Username, employee.Roles);
        }
        else
        {
            var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == accountId, ct);
            if (client is null || client.AccountStatus != ClientAccountStatus.Approved)
                return null;
            return new TokenSubject(SessionAccountType.Client, client.Id, client.Username ?? string.Empty, new List<EmployeeRole>());
        }
    }

    private string CreateAccessToken(TokenSubject subject, out DateTimeOffset expiresAt)
    {
        expiresAt = DateTimeOffset.UtcNow.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subject.AccountId.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, subject.Username),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(DaftechClaimTypes.AccountType, subject.AccountType.ToString()),
        };

        foreach (var role in subject.Roles)
            claims.Add(new Claim(ClaimTypes.Role, role.ToString()));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static (string RawToken, string Hash) GenerateRefreshToken()
    {
        var rawBytes = RandomNumberGenerator.GetBytes(64);
        var rawToken = Convert.ToBase64String(rawBytes);
        return (rawToken, HashToken(rawToken));
    }

    private static string HashToken(string rawToken)
    {
        var bytes = Encoding.UTF8.GetBytes(rawToken);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
