namespace DaftechCrm.Domain.Entities;

/// <summary>
/// Which admin-managed dropdown a LocationEntry row belongs to. Region /
/// Zone / Woreda form a strict parent chain (Region -> Zone -> Woreda) via
/// ParentId — see LocationEntry.ParentId. City remains a flat list,
/// independent of the Region/Zone/Woreda chain, alongside it (not a rename
/// of Zone). Specialization populates the Employee Specialization field,
/// replacing free text. CustomRole populates the extra, purely-descriptive
/// role checkboxes on the Employee form — see Employee.ExtraRoleLabels;
/// these carry NO authorization meaning and are entirely separate from the
/// hardcoded EmployeeRole enum, which still drives every [Authorize]
/// policy in the app unchanged.
/// </summary>
public enum LocationType { Region, Zone, City, Woreda, Specialization, CustomRole }

/// <summary>
/// One admin-managed option in a dropdown/checklist shown on the client or
/// employee forms. A single flat lookup table shared by all six list
/// types (Type distinguishes them). Region/Zone/Woreda additionally chain
/// through ParentId: a Zone's ParentId points at its owning Region, a
/// Woreda's ParentId points at its owning Zone. Region, City,
/// Specialization, and CustomRole rows always have ParentId == null. No FK
/// onto Client or Employee, so existing client records (which store their
/// own Region/Zone/Woreda as plain strings) are unaffected; this table
/// only supplies the allowed options, their hierarchy, and light
/// uniqueness validation per type.
/// </summary>
public class LocationEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public LocationType Type { get; set; }
    public string Name { get; set; } = default!;

    /// <summary>
    /// Owning Region (for a Zone) or owning Zone (for a Woreda). Always
    /// null for Region/City/Specialization/CustomRole rows. Self-referencing
    /// FK onto this same table, Cascade on delete — removing a Region
    /// removes its Zones, which in turn removes their Woredas.
    /// </summary>
    public Guid? ParentId { get; set; }
    public LocationEntry? Parent { get; set; }
}
