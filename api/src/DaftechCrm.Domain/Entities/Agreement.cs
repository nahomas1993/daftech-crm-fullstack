using DaftechCrm.Domain.Enums;

namespace DaftechCrm.Domain.Entities;

/// <summary>
/// One agreement, signed for a specific Client → SystemProduct, under a
/// specific AgreementType. Support is the primary type in active use —
/// training is no longer modeled as a signed agreement at all (see
/// SystemProduct.TrainingAssignments/TrainingRecords/TrainingCompletionStatus),
/// so a real deployment should only ever see Support (and any other
/// admin-defined non-training type) agreements created going forward. A
/// SystemProduct can carry multiple agreements over time (e.g. several
/// Support agreements as each expires and is renewed) — creating a new
/// Agreement never overwrites or replaces an existing one; each has its
/// own SignDate/dates/status/details and its own row (see
/// AgreementService.CreateAsync, which only ever inserts).
///
/// Client is still reachable via SystemProduct.Client, but no longer
/// stored directly on Agreement — that FK moved to SystemProduct when the
/// SystemProduct layer was introduced (see SeedData/migration
/// 20260819000000_AddSystemProductAndAgreementType, which backfills one
/// SystemProduct per client for every pre-existing agreement).
/// </summary>
public class Agreement
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SystemProductId { get; set; }
    public SystemProduct SystemProduct { get; set; } = default!;

    public Guid AgreementTypeId { get; set; }
    public AgreementType AgreementType { get; set; } = default!;

    public string DocumentNumber { get; set; } = default!;
    public string? ScannedFileUrl { get; set; }
    public string AgreementPlace { get; set; } = default!;

    /// <summary>
    /// The date this agreement was signed. Admin-entered directly (no
    /// longer forced to "today" — see requirement: Admin can record the
    /// actual signed date, including backdating a paper agreement signed
    /// earlier). Still defaults to today in the UI as a convenience.
    /// </summary>
    public DateOnly SignDate { get; set; }

    public DateOnly ExpiryDate { get; set; }
    public int SupportWindowMonths { get; set; } = 12;
    public AgreementStatus Status { get; set; } = AgreementStatus.Active;
    public BillingTier BillingTier { get; set; }

    /// <summary>Free-text notes/details specific to this agreement — e.g. scope clarifications, special terms. Optional.</summary>
    public string? Details { get; set; }

    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();

    /// <summary>
    /// A ticket raised against this agreement is Free while today falls within
    /// [SignDate, SignDate + SupportWindowMonths]; Chargeable afterward.
    /// Mirrors the frontend's AgreementService.isWithinSupportWindow so both
    /// sides agree on the derived chargeable flag.
    /// </summary>
    public bool IsWithinSupportWindow(DateOnly onDate)
    {
        var windowEnd = SignDate.AddMonths(SupportWindowMonths);
        return onDate >= SignDate && onDate <= windowEnd;
    }
}
