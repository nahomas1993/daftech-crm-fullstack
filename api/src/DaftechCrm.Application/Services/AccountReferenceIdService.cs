using System.Security.Cryptography;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DaftechCrm.Application.Services;

/// <summary>
/// Generates the permanent, human-readable account identifier shown in the
/// UI and used to distinguish account types at a glance — "DAF-ADMIN-1234",
/// "DAF-EMP-5678", "DAF-CLI-9012". Exactly 4 random digits per the
/// requirement (not sequential, not year-stamped — unlike
/// ReferenceNumberService's Client IdNumber / Agreement DocumentNumber,
/// which is a different, pre-existing identifier).
///
/// This ID is set once at account creation and is never regenerated or
/// edited afterward — callers must not call this again for an existing
/// account. It carries no authorization meaning by itself: every
/// [Authorize] policy in the app checks the account's real stored Role(s),
/// never this string, so a client editing/guessing an ID cannot escalate
/// privilege (see AuthorizationPolicies).
/// </summary>
public class AccountReferenceIdService
{
    private readonly IAppDbContext _db;

    public AccountReferenceIdService(IAppDbContext db) => _db = db;

    public Task<string> GenerateForClientAsync(CancellationToken ct = default) =>
        GenerateAsync("CLI", ct);

    /// <summary>Prefix depends on whether Admin is among the employee's roles at creation time.</summary>
    public Task<string> GenerateForEmployeeAsync(IReadOnlyList<EmployeeRole> roles, CancellationToken ct = default) =>
        GenerateAsync(roles.Contains(EmployeeRole.Admin) ? "ADMIN" : "EMP", ct);

    private async Task<string> GenerateAsync(string prefix, CancellationToken ct)
    {
        var attempts = 0;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var digits = RandomNumberGenerator.GetInt32(0, 10000);
            var candidate = $"DAF-{prefix}-{digits:D4}";

            if (!await ExistsAsync(candidate, ct))
                return candidate;

            attempts++;
            if (attempts > 50)
                throw new InvalidOperationException($"Could not generate a unique {prefix} account ID after 50 attempts.");
        }
    }

    /// <summary>
    /// Checked across both Employees and Clients (not just the table being
    /// inserted into) so the same DAF-XXX-#### value can never appear
    /// twice in the system even across account types.
    /// </summary>
    private async Task<bool> ExistsAsync(string candidate, CancellationToken ct)
    {
        var employeeTaken = await _db.Employees.AnyAsync(e => e.AccountRefId == candidate, ct);
        if (employeeTaken) return true;
        return await _db.Clients.AnyAsync(c => c.AccountRefId == candidate, ct);
    }
}
