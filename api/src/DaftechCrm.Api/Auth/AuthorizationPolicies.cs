using DaftechCrm.Domain.Enums;
using DaftechCrm.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;

namespace DaftechCrm.Api.Auth;

/// <summary>
/// Named authorization policies used across controllers. Employee roles
/// (Admin/EmployeeTechnician) are carried as standard role claims; "is
/// this caller an Employee at all vs a Client" is carried separately via
/// the daftech_account_type claim, since a Client has no EmployeeRole but
/// still needs to be distinguished from anonymous.
///
/// The ItSupport role has been retired — the Admin now handles everything
/// ItSupport used to do (ticket triage is automatic on submission; see
/// TicketService.SubmitFromClientAsync). EmployeeRole.ItSupport itself is
/// left in the enum rather than deleted, since Roles is stored as a
/// delimited string (not a real FK) and removing the enum value would
/// throw on any existing employee row that still has it — the intent is
/// simply that no policy or code path treats it specially anymore.
/// </summary>
public static class AuthorizationPolicies
{
    public const string AnyEmployee = "AnyEmployee";
    public const string AdminOnly = "AdminOnly";

    /// <summary>
    /// Kept as a distinct name (rather than replaced by AdminOnly at every
    /// call site) so it stays easy to grep for every endpoint that used to
    /// be ItSupport-reachable, in case that access needs to be reconsidered
    /// later. Functionally identical to AdminOnly today.
    /// </summary>
    public const string AdminOrItSupport = "AdminOnly";

    public const string AnyClient = "AnyClient";
    public const string AnyAuthenticated = "AnyAuthenticated";

    public static void AddDaftechPolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(AnyAuthenticated, p => p.RequireAuthenticatedUser());

        options.AddPolicy(AnyEmployee, p => p.RequireClaim(DaftechClaimTypes.AccountType, nameof(SessionAccountType.Employee)));

        options.AddPolicy(AdminOnly, p => p
            .RequireClaim(DaftechClaimTypes.AccountType, nameof(SessionAccountType.Employee))
            .RequireRole(nameof(EmployeeRole.Admin)));

        options.AddPolicy(AnyClient, p => p.RequireClaim(DaftechClaimTypes.AccountType, nameof(SessionAccountType.Client)));
    }
}
