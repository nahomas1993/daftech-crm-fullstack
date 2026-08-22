using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Options;
using DaftechCrm.Domain.Entities;
using DaftechCrm.Domain.Enums;
using DaftechCrm.Infrastructure.Auth;
using DaftechCrm.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace DaftechCrm.Tests.Auth;

/// <summary>
/// Covers TokenService's core security invariants: a refresh rotates the
/// token (old one no longer works), and reusing an already-rotated token
/// is treated as theft and revokes every other active session for that
/// account. Uses EF Core's InMemory provider — fast, no real PostgreSQL needed
/// for these logic-only checks.
/// </summary>
public class TokenServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly TokenService _sut;

    public TokenServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        var jwtOptions = Options.Create(new JwtOptions
        {
            SigningKey = "unit-test-signing-key-at-least-32-bytes-long!!",
            Issuer = "DaftechCrm.Tests",
            Audience = "DaftechCrm.Tests.Client",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 14,
        });

        _sut = new TokenService(_db, jwtOptions, NullLogger<TokenService>.Instance);
    }

    private static TokenSubject SampleEmployeeSubject(Guid id) =>
        new(SessionAccountType.Employee, id, "jdoe", new List<EmployeeRole> { EmployeeRole.ItSupport });

    [Fact]
    public async Task IssueTokenPairAsync_persists_a_hashed_refresh_token_not_the_raw_value()
    {
        var employeeId = Guid.NewGuid();
        var pair = await _sut.IssueTokenPairAsync(SampleEmployeeSubject(employeeId), "127.0.0.1");

        var stored = await _db.RefreshTokens.SingleAsync();
        stored.TokenHash.Should().NotBe(pair.RefreshTokenPlainText);
        stored.TokenHash.Should().HaveLength(64); // SHA-256 hex string
        stored.AccountId.Should().Be(employeeId);
        stored.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshAsync_rotates_the_token_so_the_old_one_no_longer_works()
    {
        var employeeId = Guid.NewGuid();
        _db.Add(new Employee
        {
            Id = employeeId, Username = "jdoe", FullName = "J Doe", Email = "j@daftech.et",
            PasswordHash = "irrelevant-for-this-test", Roles = new List<EmployeeRole> { EmployeeRole.ItSupport },
            AccountStatus = EmployeeAccountStatus.Active, AccountRefId = "DAF-EMP-0001",
        });
        await _db.SaveChangesAsync();

        var original = await _sut.IssueTokenPairAsync(SampleEmployeeSubject(employeeId), "127.0.0.1");

        var refreshed = await _sut.RefreshAsync(original.RefreshTokenPlainText, "127.0.0.1");
        refreshed.RefreshTokenPlainText.Should().NotBe(original.RefreshTokenPlainText);

        // The original token has been rotated (revoked) — using it again must fail.
        var act = async () => await _sut.RefreshAsync(original.RefreshTokenPlainText, "127.0.0.1");
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Reusing_a_rotated_refresh_token_revokes_every_other_active_session_for_the_account()
    {
        var employeeId = Guid.NewGuid();
        _db.Add(new Employee
        {
            Id = employeeId, Username = "jdoe", FullName = "J Doe", Email = "j@daftech.et",
            PasswordHash = "irrelevant-for-this-test", Roles = new List<EmployeeRole> { EmployeeRole.ItSupport },
            AccountStatus = EmployeeAccountStatus.Active, AccountRefId = "DAF-EMP-0002",
        });
        await _db.SaveChangesAsync();

        // Simulate two active sessions (e.g. two devices) for the same account.
        var sessionA = await _sut.IssueTokenPairAsync(SampleEmployeeSubject(employeeId), "127.0.0.1");
        var sessionB = await _sut.IssueTokenPairAsync(SampleEmployeeSubject(employeeId), "10.0.0.5");

        // Session A refreshes normally (rotates its token)...
        await _sut.RefreshAsync(sessionA.RefreshTokenPlainText, "127.0.0.1");

        // ...then the ORIGINAL (now-stale) token A is reused — e.g. an
        // attacker replaying a stolen token. This should be detected and
        // should revoke session B too, as a precaution.
        var act = async () => await _sut.RefreshAsync(sessionA.RefreshTokenPlainText, "203.0.113.9");
        await act.Should().ThrowAsync<InvalidOperationException>();

        var sessionBToken = await _db.RefreshTokens.SingleAsync(t => t.TokenHash == HashForTest(sessionB.RefreshTokenPlainText));
        sessionBToken.IsActive.Should().BeFalse("reuse of a rotated token should revoke all other active sessions for the account");
    }

    private static string HashForTest(string rawToken)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(rawToken);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    public void Dispose() => _db.Dispose();
}
