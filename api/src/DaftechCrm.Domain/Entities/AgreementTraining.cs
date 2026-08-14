namespace DaftechCrm.Domain.Entities;

/// <summary>
/// A single client-training record. Belongs to a Client directly (not to
/// an Agreement) because training happens BEFORE any support agreement
/// exists — it's the mandatory prerequisite that must finish before
/// Daftech and the client sign the support agreement (see
/// AgreementService.CreateAsync). AgreementId starts null and is only
/// filled in once an agreement is later signed for that client (at which
/// point the completed trainings that made signing possible get linked to
/// it, for record-keeping — see Agreement.Trainings).
/// </summary>
public class AgreementTraining
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ClientId { get; set; }
    public Client Client { get; set; } = default!;

    /// <summary>Null until a support agreement is signed for this client (see AgreementService.CreateAsync). A training is recorded independently of any agreement.</summary>
    public Guid? AgreementId { get; set; }
    public Agreement? Agreement { get; set; }

    /// <summary>Free-text description of what was covered, who attended, etc. Max 1000 characters (see AgreementService.SaveTrainingAsync).</summary>
    public string? Description { get; set; }

    public DateOnly? StartDate { get; set; }

    /// <summary>
    /// When this training finished. Filled in once training is done —
    /// this is what "training complete" means (see
    /// AgreementService.CreateAsync's completed-training check). Left
    /// editable afterward so the admin can push it out if training runs
    /// long for unforeseen reasons, without needing a separate status flag.
    /// </summary>
    public DateOnly? EndDate { get; set; }

    /// <summary>Storage key (per IFileStorageService) for the scanned training document. Null until uploaded.</summary>
    public string? ScanStorageKey { get; set; }
    public string? ScanFileName { get; set; }
}
