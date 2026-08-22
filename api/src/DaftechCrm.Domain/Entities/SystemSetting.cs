namespace DaftechCrm.Domain.Entities;

/// <summary>
/// Admin-editable system configuration, stored as key/value pairs so new
/// settings can be added without a schema change. Values are always
/// stored as strings and parsed by whatever consumes them (see
/// SystemConfigurationService for the typed accessors). A row only
/// exists once an Admin has actually changed a value away from its
/// appsettings.json default — GetAllAsync fills in unset keys with their
/// defaults so the Configuration page always shows the full set.
/// </summary>
public class SystemSetting
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Stable key, e.g. "TicketWorkflow.OnTimeResolutionTargetDays". Unique.</summary>
    public string Key { get; set; } = default!;

    public string Value { get; set; } = default!;

    /// <summary>Which Settings page section this belongs under, e.g. "Ticket Workflow", "Sessions & Devices".</summary>
    public string Category { get; set; } = default!;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Full name of the Admin who last changed this value, for the audit trail on the Configuration page.</summary>
    public string? UpdatedByName { get; set; }
}
