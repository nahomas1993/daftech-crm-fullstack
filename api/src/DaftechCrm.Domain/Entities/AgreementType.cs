namespace DaftechCrm.Domain.Entities;

/// <summary>
/// Well-known AgreementType.Name values that always exist (seeded — see
/// SeedData) and that the rest of the app can reason about by name.
/// Support is the one the training-before-support gate (see
/// AgreementService.CreateAsync) checks by name — Training is kept as a
/// seeded lookup value for Admin convenience, but the training workflow
/// itself no longer runs through Agreement/AgreementType at all; it lives
/// directly on SystemProduct (see SystemProduct.TrainingAssignments/
/// TrainingRecords/TrainingCompletionStatus). Admins may add further
/// custom agreement types beyond these two; nothing in the app requires
/// the set to stay limited to these.
/// </summary>
public static class AgreementTypeNames
{
    public const string Support = "Support";
    public const string Training = "Training";
}

/// <summary>
/// An admin-managed kind of agreement (e.g. "Support") that an Agreement
/// is signed under, for a given Client → SystemProduct. Modeled as its
/// own lookup table (matching the FailureType/LocationEntry pattern)
/// rather than a closed enum so an Admin can add more agreement types
/// later without a code change. Support always exists (seeded — see
/// SeedData) since the app's business rule (training must be marked
/// Completed on a SystemProduct before a Support agreement can be signed
/// for it — see AgreementService.CreateAsync) depends on it by name.
/// Training also exists as a seeded lookup value, kept for convenience,
/// but no business rule keys off it — see AgreementTypeNames.
/// </summary>
public class AgreementType
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = default!;
    public string? Description { get; set; }

    /// <summary>
    /// True for the two seeded types (Support, Training) — the UI hides
    /// the delete action for these so an Admin can't accidentally remove
    /// a type other parts of the app reference by name (Support) or that
    /// existing historical agreements may still use (Training). Custom
    /// types an Admin adds afterward are deletable.
    /// </summary>
    public bool IsSystemDefined { get; set; }

    public ICollection<Agreement> Agreements { get; set; } = new List<Agreement>();
}
