using DaftechCrm.Domain.Entities;

namespace DaftechCrm.Application.Interfaces;

/// <summary>
/// Assigns tickets automatically. There is no Admin override: as soon as a
/// ticket is forwarded, the employee with the fewest open tickets (among
/// Active Employee/Technician staff) is assigned. If two employees tie on
/// open-ticket count, the one who has gone longest without a new
/// assignment is picked, keeping the rotation fair.
/// </summary>
public interface ITicketAssignmentService
{
    /// <summary>
    /// Returns the employee who should receive the next ticket, or null if
    /// no eligible employee exists. When requiredSpecialization is given,
    /// candidates are first narrowed to active technicians whose
    /// Specialization matches it (case-insensitive) before applying the
    /// workload/last-assignment ordering; if no specialist is currently
    /// active, falls back to ranking every active technician exactly as
    /// before this parameter existed. Null/blank requiredSpecialization
    /// skips the filter entirely — unchanged behavior for tickets with no
    /// resolved specialty (e.g. legacy tickets).
    /// </summary>
    Task<Employee?> SelectAssigneeAsync(string? requiredSpecialization = null, CancellationToken ct = default);
}
