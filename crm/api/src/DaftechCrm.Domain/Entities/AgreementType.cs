namespace DaftechCrm.Domain.Entities;

/// <summary>
/// Well-known AgreementType.Name values that always exist (seeded — see
/// SeedData) and that the rest of the app can reason about by name, e.g.
/// the training-before-support gate in AgreementService. Admins may add
/// further custom agreement types beyond these two; nothing in the app
/// requires the set to stay limited to these.
/// </summary>
public static class AgreementTypeNames
{
    public const string Support = "Support";
    public const string Training = "Training";
}

/// <summary>
/// An admin-managed kind of agreement (e.g. "Support", "Training") that an
/// Agreement is signed under, for a given Client → SystemProduct. Modeled
/// as its own lookup table (matching the FailureType/LocationEntry
/// pattern) rather than a closed enum so an Admin can add more agreement
/// types later without a code change. Support and Training always exist
/// (seeded — see SeedData) since the app's business rules (training must
/// complete before a support agreement can be signed for the same
/// SystemProduct — see AgreementService.CreateAsync) depend on them by
/// name.
/// </summary>
public class AgreementType
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = default!;
    public string? Description { get; set; }

    /// <summary>
    /// True for the two seeded, business-rule-relevant types (Support,
    /// Training) — the UI hides the delete action for these so an Admin
    /// can't accidentally remove a type the app's workflow logic depends
    /// on by name. Custom types an Admin adds afterward are deletable.
    /// </summary>
    public bool IsSystemDefined { get; set; }

    public ICollection<Agreement> Agreements { get; set; } = new List<Agreement>();
}
