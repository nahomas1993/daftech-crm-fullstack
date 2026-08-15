namespace DaftechCrm.Domain.Entities;

/// <summary>Unit the Admin chose when setting a FailureType's expected resolution time.</summary>
public enum DurationUnit { Hours, Days, Months }

/// <summary>
/// An admin-defined kind of client-system failure (e.g. "Server Down",
/// "Printer Offline"), each with its own expected resolution time. Chosen
/// by the client on ticket submission (Ticket.FailureTypeId, alongside the
/// existing Category enum — additive, Category is unchanged) and used by
/// the on-time/late report in place of the single global
/// TicketWorkflowOptions.OnTimeResolutionTargetDays when a ticket has one
/// set.
/// </summary>
public class FailureType
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = default!;

    /// <summary>Optional admin-entered explanation of what this failure type covers, shown alongside the name in the admin settings list. Not shown to clients — the Submit Issue dropdown only needs the name to pick from.</summary>
    public string? Description { get; set; }

    public int DurationValue { get; set; }
    public DurationUnit DurationUnit { get; set; }

    /// <summary>Converts DurationValue/DurationUnit to a single TimeSpan for deadline math. 30 days/month, consistent with how "1 month" is commonly approximated for SLA targets — not calendar-accurate, documented so it isn't mistaken for one.</summary>
    public TimeSpan ToTimeSpan() => DurationUnit switch
    {
        DurationUnit.Hours => TimeSpan.FromHours(DurationValue),
        DurationUnit.Days => TimeSpan.FromDays(DurationValue),
        DurationUnit.Months => TimeSpan.FromDays(DurationValue * 30),
        _ => throw new InvalidOperationException($"Unknown duration unit: {DurationUnit}")
    };
}
