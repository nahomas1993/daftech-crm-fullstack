namespace DaftechCrm.Domain.Entities;

/// <summary>
/// An admin-managed catalog entry describing a kind of system/product
/// DAFTECH deploys for clients (e.g. "Branch POS System", "HR Portal"),
/// matching the FailureType/SupportType lookup pattern. Configured from
/// Settings by an Admin — no code change is needed to add, rename, or
/// retire an entry.
///
/// This is distinct from <see cref="SystemProduct"/>, which is the actual
/// per-client instance (with its own agreements/training/expiry) that a
/// client is assigned. A SystemProduct may optionally reference the
/// catalog entry it was created from (see SystemProduct.CatalogItemId) so
/// the "Add System/Product" form can offer a dropdown of admin-defined
/// names instead of free text, while still tolerating older/legacy
/// SystemProducts that only have a free-text Name.
/// </summary>
public class ProductCatalogItem
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = default!;

    /// <summary>Optional admin-entered explanation of what this system/product covers.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// Soft "retire" flag — an Admin can hide a catalog entry from new
    /// selections (registration form, system/product creation) without
    /// deleting it outright, since existing SystemProducts may still
    /// reference it. Inactive entries are excluded from the public list
    /// but remain in the database and still resolve by Id for anything
    /// already pointing at them.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
