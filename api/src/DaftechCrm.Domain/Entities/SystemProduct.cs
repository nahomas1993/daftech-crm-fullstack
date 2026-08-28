namespace DaftechCrm.Domain.Entities;

/// <summary>
/// One system/product Daftech has deployed or supports for a client (e.g.
/// "Branch POS System", "HR Portal"). Sits between Client and Agreement:
///
///   Client → SystemProduct → Agreement → AgreementType
///
/// Training lives directly on SystemProduct (TrainingAssignments,
/// TrainingRecords, TrainingCompletionStatus below) rather than as an
/// Agreement — unlike Support, training is never itself a signed
/// document; it's tracked activity that gates whether a Support agreement
/// can later be signed for this SAME system/product (see
/// AgreementService.CreateAsync).
///
/// A client can have multiple systems/products, and each one can carry
/// its own set of agreements (a Support agreement, etc.) with their own
/// dates/status/details — creating a new SystemProduct or Agreement never
/// overwrites an existing one, they simply accumulate under the client.
/// Introduced together with AgreementType to replace the earlier flat
/// Client → Agreement model (see AgreementService for the migration of
/// pre-existing agreements into a per-client "General" SystemProduct).
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

    /// <summary>
    /// Optional reference back to the admin-managed Systems/Products
    /// catalog (see <see cref="ProductCatalogItem"/>) this entry was
    /// created from. Null for SystemProducts created before the catalog
    /// existed, or when an Admin typed a free-text Name instead of
    /// picking a catalog entry — Name always stays the source of truth
    /// for display, this is purely for traceability/reporting.
    /// </summary>
    public Guid? CatalogItemId { get; set; }
    public ProductCatalogItem? CatalogItem { get; set; }

    /// <summary>When this system/product was deployed/onboarded for the client. Purely informational — does not gate anything.</summary>
    public DateOnly? DeploymentDate { get; set; }

    /// <summary>
    /// When this specific client's system/product is due to expire (e.g.
    /// license/subscription/support end date) — shown on the client
    /// dashboard so a client can see, per product, when it runs out.
    /// Distinct from Agreement.ExpiryDate (which tracks a signed support
    /// agreement's own expiry) — a system/product can have an expiry even
    /// before any agreement exists for it. Optional: not every
    /// system/product has one.
    /// </summary>
    public DateOnly? ExpiryDate { get; set; }

    /// <summary>
    /// Soft-delete flag. Agreements reference SystemProductId, so a real
    /// DELETE would either orphan them or be blocked by FK constraints —
    /// setting this instead removes the system/product from active lists
    /// while keeping its agreement history intact.
    /// </summary>
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public ICollection<Agreement> Agreements { get; set; } = new List<Agreement>();

    /// <summary>Every Trainer/Technician currently assigned to train on this system/product — see TrainingAssignment.</summary>
    public ICollection<TrainingAssignment> TrainingAssignments { get; set; } = new List<TrainingAssignment>();

    /// <summary>The open-ended log of training sessions actually conducted — see TrainingRecord.</summary>
    public ICollection<TrainingRecord> TrainingRecords { get; set; } = new List<TrainingRecord>();

    /// <summary>
    /// Set by SystemProductService.MarkTrainingCompletedAsync — a one-click
    /// Admin decision based on the accumulated TrainingRecords, not derived
    /// automatically from any count/threshold. Completed unlocks signing a
    /// Support agreement for this SAME system/product (see
    /// AgreementService.CreateAsync) but doesn't stop more TrainingRecords
    /// being logged afterward (e.g. a refresher) — see MarkTrainingCompletedAsync.
    /// </summary>
    public TrainingCompletionStatus TrainingCompletionStatus { get; set; } = TrainingCompletionStatus.NotStarted;

    /// <summary>
    /// Stamped by SystemProductService.SubmitTrainingAsync — a Trainer's
    /// own signal that they've saved a TrainingRecord for every
    /// agreement item on their checklist and are done, distinct from
    /// Admin's separate MarkTrainingCompletedAsync review/sign-off. Null
    /// until a Trainer submits; a later submit (e.g. after a refresher)
    /// simply overwrites this timestamp.
    /// </summary>
    public DateTimeOffset? TrainingSubmittedAt { get; set; }
}
