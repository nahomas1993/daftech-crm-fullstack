using DaftechCrm.Domain.Enums;

namespace DaftechCrm.Application;

/// <summary>
/// Enforces which EmployeeRole combinations are valid for one employee.
/// Admin is a distinct account tier (full system access) and must never
/// be combined with an operational role (EmployeeTechnician/Trainer,
/// ItSupport) — those roles gate what a technician-level account can do
/// day to day, and stacking Admin on top of them would make "is this an
/// Admin account" ambiguous everywhere permissions are checked.
/// EmployeeTechnician and Trainer, on the other hand, are meant to
/// combine freely — an employee can hold both at once (see
/// EmployeeService.SetResponsibilitiesAsync's own comment on this).
///
/// Used by EmployeeService.RegisterAsync and SetResponsibilitiesAsync so
/// the rule holds server-side even if the Angular checkbox UI (see
/// EmployeesComponent) is bypassed or reaches the API directly — same
/// defense-in-depth relationship as RequiredFieldValidator.
/// </summary>
public static class EmployeeRoleValidator
{
    /// <summary>
    /// Throws a <see cref="ValidationException"/> if Admin is combined
    /// with any other role. No-op for an empty/single-role list or any
    /// non-Admin combination (e.g. [EmployeeTechnician, Trainer] is fine).
    /// </summary>
    public static void EnsureValidCombination(IReadOnlyCollection<EmployeeRole> roles)
    {
        if (roles.Contains(EmployeeRole.Admin) && roles.Count > 1)
            throw new ValidationException("Admin cannot be combined with other roles — choose Admin on its own, or Technician/Trainer instead.");
    }
}
