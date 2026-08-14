namespace DaftechCrm.Domain.Entities;

/// <summary>
/// Which admin-managed dropdown a LocationEntry row belongs to. Region /
/// City / Woreda populate the client Region/City/Woreda fields (flat
/// lists, no hierarchy). Specialization populates the Employee
/// Specialization field, replacing free text. CustomRole populates the
/// extra, purely-descriptive role checkboxes on the Employee form — see
/// Employee.ExtraRoleLabels; these carry NO authorization meaning and are
/// entirely separate from the hardcoded EmployeeRole enum, which still
/// drives every [Authorize] policy in the app unchanged.
/// </summary>
public enum LocationType { Region, City, Woreda, Specialization, CustomRole }

/// <summary>
/// One admin-managed option in a dropdown/checklist shown on the client or
/// employee forms. A single flat lookup table shared by all five list
/// types (Type distinguishes them) — no hierarchy, no FK onto Client or
/// Employee, so existing rows and registration flows are unaffected; this
/// table only supplies the allowed options and light uniqueness
/// validation per type.
/// </summary>
public class LocationEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public LocationType Type { get; set; }
    public string Name { get; set; } = default!;
}
