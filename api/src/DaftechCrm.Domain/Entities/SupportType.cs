namespace DaftechCrm.Domain.Entities;

/// <summary>
/// An admin-defined kind of support delivery (e.g. "Remote", "On-site",
/// "Emergency after hours"), each with an extra fee on top of the failure
/// type's base price. A client picks one when submitting an issue; if their
/// free support window has already run out, the ticket is priced as
/// FailureType.BasePrice + SupportType.AdditionalFee (see
/// TicketService.SubmitFromClientAsync).
/// </summary>
public class SupportType
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = default!;

    /// <summary>Optional note explaining what this support type covers. Shown to admins in Settings and to clients under the dropdown.</summary>
    public string? Description { get; set; }

    /// <summary>Extra charge in ETB added to the failure type's base price. Zero is allowed — some support types cost nothing extra.</summary>
    public decimal AdditionalFee { get; set; }
}
