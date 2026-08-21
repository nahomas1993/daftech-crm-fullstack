namespace DaftechCrm.Domain.Entities;

/// <summary>
/// One system/product Daftech has deployed or supports for a client (e.g.
/// "Branch POS System", "HR Portal"). Sits between Client and Agreement:
///
///   Client → SystemProduct → Agreement → AgreementType
///
/// A client can have multiple systems/products, and each one can carry
/// its own set of agreements (a Support agreement, a Training agreement,
/// etc.) with their own dates/status/details — creating a new
/// SystemProduct or Agreement never overwrites an existing one, they
/// simply accumulate under the client. Introduced together with
/// AgreementType to replace the earlier flat Client → Agreement model
/// (see AgreementService for the migration of pre-existing agreements
/// into a per-client "General" SystemProduct).
/// </summary>
public class SystemProduct
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ClientId { get; set; }
    public Client Client { get; set; } = default!;

    /// <summary>Human-readable, permanent reference — "DAF-SYS-YYYY-####" (see ReferenceNumberService.GenerateSystemProductRefAsync).</summary>
    public string ReferenceNumber { get; set; } = default!;

    public string Name { get; set; } = default!;
    public string? Description { get; set; }

    /// <summary>When this system/product was deployed/onboarded for the client. Purely informational — does not gate anything.</summary>
    public DateOnly? DeploymentDate { get; set; }

    /// <summary>
    /// Soft-delete flag. Agreements reference SystemProductId, so a real
    /// DELETE would either orphan them or be blocked by FK constraints —
    /// setting this instead removes the system/product from active lists
    /// while keeping its agreement history intact.
    /// </summary>
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public ICollection<Agreement> Agreements { get; set; } = new List<Agreement>();
}
