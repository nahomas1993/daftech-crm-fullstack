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


public record ExpiringClientDto(Guid ClientId, string ClientName, Guid AgreementId, string SystemProductName, DateOnly ExpiryDate, int DaysUntilExpiry);
public record SupportClientDto(Guid ClientId, string ClientName, int TicketCount);

public record SupportOverviewDto(
    int ApproachingExpirationCount,
    int FreeSupportClientCount,
    int ChargeableSupportClientCount,
    IReadOnlyList<ExpiringClientDto> ApproachingExpiration,
    IReadOnlyList<SupportClientDto> FreeSupportClients,
    IReadOnlyList<SupportClientDto> ChargeableSupportClients
);

// --- Dashboard (charts + KPIs only — see TicketReportDtos.cs for the
// Reports module's table-only DTOs; the two are intentionally separate
// per the product's Reports-vs-Dashboard split) ---

/// <summary>The Dashboard's KPI cards — total tickets, open, resolved, overdue, resolution rate, and customer satisfaction, all computed over whatever DashboardFilter the caller supplied.</summary>
public record DashboardKpisDto(
    int TotalTickets, int OpenTickets, int ResolvedTickets, int OverdueTickets,
    double ResolutionRatePercent, double? AverageSatisfactionScore
);

/// <summary>One bar in the "tickets by region" chart.</summary>
public record RegionTicketCountDto(string Region, int TicketCount);

/// <summary>One bar in the "tickets by failure type" chart.</summary>
public record FailureTypeTicketCountDto(string FailureTypeName, int TicketCount);

/// <summary>One bar in the "employee performance" chart — resolved-ticket count per employee, for a quick at-a-glance comparison (the Reports module's Employee Performance table has the full breakdown).</summary>
public record EmployeeTicketCountDto(string EmployeeName, int ResolvedCount);

/// <summary>One point in a monthly trend line — Month is "yyyy-MM" so points sort and label naturally regardless of year boundaries.</summary>
public record MonthlyPointDto(string Month, int TicketCount, int ResolvedCount, double? OnTimeRatePercent);

/// <summary>One slice of the customer-rating distribution donut. Stars is in half-star increments (1, 1.5, 2, ..., 5) since half-star ratings are allowed — see Ticket.SatisfactionStars.</summary>
public record RatingSliceDto(decimal Stars, int Count);

/// <summary>
/// Everything the Dashboard needs in one call — bar/donut/line chart data
/// plus KPI cards, all computed over the same DashboardFilter so every
/// chart on the page reflects the same filtered slice of tickets. See
/// IReportService.GetDashboardDataAsync.
/// </summary>
public record DashboardDataDto(
    DashboardKpisDto Kpis,
    IReadOnlyList<RegionTicketCountDto> TicketsByRegion,
    IReadOnlyList<FailureTypeTicketCountDto> TicketsByFailureType,
    IReadOnlyList<EmployeeTicketCountDto> TicketsByEmployee,
    IReadOnlyList<TicketStatusSliceDto> TicketsByStatus,
    IReadOnlyList<RatingSliceDto> RatingDistribution,
    IReadOnlyList<MonthlyPointDto> MonthlyTrend,
    SupportOverviewDto SupportOverview
);

/// <summary>
/// Filters the Dashboard's charts/KPIs can be scoped by — a small subset
/// of TicketReportFilter's dimensions (date range + region), since the
/// Dashboard is a live at-a-glance view, not the deep per-dimension drill
/// -down the Reports module's six tables provide.
/// </summary>
public record DashboardFilter(DateOnly? FromDate, DateOnly? ToDate, string? Region);
