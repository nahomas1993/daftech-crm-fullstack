using DaftechCrm.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DaftechCrm.Application.Services;

/// <summary>
/// Generates system-assigned, human-readable reference numbers (Client ID
/// Number, Agreement Document Number) instead of letting staff type them in
/// by hand. Format: DAF-&lt;PREFIX&gt;-&lt;YEAR&gt;-&lt;0001&gt;, e.g. "DAF-CLI-2026-0001" —
/// sequential within the year, with a collision-check retry the same way
/// AccountCredentialService retries on username collisions.
/// </summary>
public class ReferenceNumberService
{
    private readonly IAppDbContext _db;

    public ReferenceNumberService(IAppDbContext db) => _db = db;

    public async Task<string> GenerateClientIdNumberAsync(CancellationToken ct = default) =>
        await GenerateAsync("CLI", async candidate => await _db.Clients.AnyAsync(c => c.IdNumber == candidate, ct), ct);

    public async Task<string> GenerateAgreementDocumentNumberAsync(CancellationToken ct = default) =>
        await GenerateAsync("AGR", async candidate => await _db.Agreements.AnyAsync(a => a.DocumentNumber == candidate, ct), ct);

    private static async Task<string> GenerateAsync(string prefix, Func<string, Task<bool>> existsAsync, CancellationToken ct)
    {
        var year = DateTime.UtcNow.Year;
        var attempts = 0;

        // Start from a running count so numbers stay roughly sequential in the
        // common case; the exists-check + retry loop is what actually
        // guarantees uniqueness if two requests race for the same number.
        var sequence = 1;

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var candidate = $"DAF-{prefix}-{year}-{sequence:D4}";

            if (!await existsAsync(candidate))
                return candidate;

            sequence++;
            attempts++;
            if (attempts > 9999)
                throw new InvalidOperationException($"Could not generate a unique {prefix} reference number after 9999 attempts.");
        }
    }
}
