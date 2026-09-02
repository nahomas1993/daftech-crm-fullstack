using DaftechCrm.Domain.Enums;

namespace DaftechCrm.Domain.Entities;

public class MaintenanceRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateOnly Date { get; set; }
    public string Category { get; set; } = default!;
    public string Description { get; set; } = default!;

    public Guid PerformedByEmployeeId { get; set; }
    public Employee PerformedByEmployee { get; set; } = default!;

    public MaintenanceStatus Status { get; set; } = MaintenanceStatus.InProgress;
    public string? Remarks { get; set; }

    /// <summary>
    /// Which client this maintenance visit was for. Nullable only to
    /// preserve legacy rows logged before maintenance records were linked
    /// to a client — every new record created via MaintenanceService
    /// requires one. Drives the client-scoped maintenance-history view.
    /// </summary>
    public Guid? ClientId { get; set; }
    public Client? Client { get; set; }

    /// <summary>Which of the client's systems/products this visit concerned, if any. When set, must belong to ClientId (validated in MaintenanceService.CreateAsync).</summary>
    public Guid? SystemProductId { get; set; }
    public SystemProduct? SystemProduct { get; set; }

    /// <summary>The support ticket this maintenance visit relates to, if it was ticket-driven rather than routine/preventive. When set, must belong to ClientId.</summary>
    public Guid? TicketId { get; set; }
    public Ticket? Ticket { get; set; }
}
