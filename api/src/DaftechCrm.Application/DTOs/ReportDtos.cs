namespace DaftechCrm.Application.DTOs;

/// <summary>One bar in the per-employee on-time-vs-late chart.</summary>
public record EmployeeOnTimeStatsDto(
    Guid EmployeeId,
    string EmployeeName,
    int OnTimeCount,
    int LateCount,
    int TotalResolved,
    double OnTimeRate // 0-100
);

/// <summary>Overall on-time vs late split, for the donut chart.</summary>
public record OnTimeSummaryDto(
    int OnTimeCount,
    int LateCount,
    int TotalResolved,
    double OnTimeRate, // 0-100
    int TargetDays
);

public record OnTimeReportDto(
    OnTimeSummaryDto Summary,
    IReadOnlyList<EmployeeOnTimeStatsDto> ByEmployee
);

/// <summary>
/// Written + graphical performance data for one employee, per SRS v2.0
/// §4.10. AiNarrative is populated only when AI reporting is enabled and
/// the call succeeds — Available/AiUnavailableReason tell the frontend
/// whether to show it or silently omit it; the rest of this DTO is always
/// populated regardless of AI availability.
/// </summary>
/// <summary>One slice of the admin operations-overview pie chart — how many tickets are currently in a given status, system-wide.</summary>
public record TicketStatusSliceDto(
    string Status,
    int Count
);

/// <summary>
/// System-wide snapshot for the admin Reports page's "Overall Operations"
/// pie chart — every ticket, grouped by its current status, plus a couple
/// of headline counts (active clients, active employees) for context.
/// Unlike OnTimeReportDto (which only looks at tickets that have already
/// been resolved), this counts every ticket regardless of status, so it
/// reflects the live state of the queue right now.
/// </summary>
public record OperationsOverviewDto(
    IReadOnlyList<TicketStatusSliceDto> TicketsByStatus,
    int TotalTickets,
    int ActiveClients,
    int ActiveEmployees,
    int OpenAgreements
);

public record EmployeePerformanceReportDto(
    Guid EmployeeId,
    string EmployeeName,
    int TicketsAssigned,
    int TicketsResolved,
    double? AverageResolutionHours,
    double OnTimeRate,
    double? AverageSatisfactionScore,
    double TotalHoursWorked,
    bool AiNarrativeAvailable,
    string? AiNarrative,
    string? AiUnavailableReason
);
