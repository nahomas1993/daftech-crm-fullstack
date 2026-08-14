using DaftechCrm.Domain.Enums;

namespace DaftechCrm.Domain.Entities;

public class Agreement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClientId { get; set; }
    public Client Client { get; set; } = default!;

    public string DocumentNumber { get; set; } = default!;
    public string? ScannedFileUrl { get; set; }
    public string AgreementPlace { get; set; } = default!;

    /// <summary>
    /// The support agreement's signing/start date. Admin-entered — set to
    /// today when the admin signs the agreement (see
    /// AgreementService.CreateAsync). An Agreement can only be created once
    /// the client has at least one completed training (a Training with
    /// EndDate set), since training is mandatory and must finish before
    /// Daftech and the client sign the support agreement — see
    /// AgreementService.CreateAsync for the enforcement of that rule.
    /// </summary>
    public DateOnly SignDate { get; set; }

    public DateOnly ExpiryDate { get; set; }
    public int SupportWindowMonths { get; set; } = 12;
    public AgreementStatus Status { get; set; } = AgreementStatus.Active;
    public BillingTier BillingTier { get; set; }

    /// <summary>
    /// Trainings delivered to this agreement's client that were already
    /// completed as of signing time (see AgreementService.CreateAsync,
    /// which links the client's completed trainings here). Not the source
    /// of truth for a client's full training history — that lives on
    /// AgreementTraining.ClientId regardless of AgreementId, since training
    /// can be recorded before any agreement exists. This collection is
    /// "which trainings had happened as of this agreement being signed."
    /// </summary>
    public ICollection<AgreementTraining> Trainings { get; set; } = new List<AgreementTraining>();

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
