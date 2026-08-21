using DaftechCrm.Domain.Entities;
using DaftechCrm.Domain.Enums;

namespace DaftechCrm.Application.DTOs;

/// <summary>
/// Shared filter set across all six table reports (see ITicketReportService).
/// Every field is optional — an unset filter simply doesn't narrow that
/// dimension. Month is separate from From/To (a coarser, single-tap filter
/// a support manager reaches for more often than picking exact dates) and
/// combines with From/To by intersection if both are given, not
/// override — see TicketReportService.ApplyFilters.
/// </summary>
public record TicketReportFilter(
    DateOnly? FromDate,
    DateOnly? ToDate,
    /// <summary>1-12. Filters DateSubmitted to that calendar month, in any year — combined with FromDate/ToDate by intersection, not override.</summary>
    int? Month,
    string? Region,
    string? Zone,
    string? Woreda,
    Guid? EmployeeId,
    Guid? FailureTypeId,
    TicketStatus? Status,
    SupportPhase? SupportPhase,
    /// <summary>Free-text search — matches against ticket description, client name, and document/reference numbers. Optional, combines with every other filter (AND, not OR).</summary>
    string? Search
);

/// <summary>One row of the Customer/Support report — a ticket with enough client/agreement context to stand alone in a printed table.</summary>
public record CustomerSupportReportRow(
    Guid TicketId, string ClientName, string? Region, string? Zone, string? Woreda,
    string? SystemProductName, string? FailureTypeName, DateTimeOffset DateSubmitted,
    string? AssignedEmployeeName, TicketStatus Status, SupportPhase SupportPhase,
    bool Chargeable, DateTimeOffset? ResolvedAt, int? SatisfactionScore
);

/// <summary>One row of the Employee Performance report — one row per employee, aggregated over tickets matching the filter.</summary>
public record EmployeePerformanceReportRow(
    Guid EmployeeId, string EmployeeName, int TotalAssigned, int Resolved, int Open,
    int Overdue, double? AverageResolutionHours, double? OnTimeRatePercent, double? AverageSatisfactionScore
);

/// <summary>One row of the Regional report — one row per Region/Zone/Woreda combination present in the filtered tickets.</summary>
public record RegionalReportRow(
    string? Region, string? Zone, string? Woreda, int TicketCount, int OpenCount, int ResolvedCount,
    double? AverageResolutionHours, double? AverageSatisfactionScore
);

/// <summary>One row of the Failure-Type report — one row per FailureType present in the filtered tickets (plus one row for tickets with no FailureType set, labeled "Unspecified").</summary>
public record FailureTypeReportRow(
    Guid? FailureTypeId, string FailureTypeName, int TicketCount, int OnTimeCount, int LateCount,
    double? OnTimeRatePercent, double? AverageResolutionHours
);

/// <summary>One row of the Resolution-Time report — one row per resolved ticket in the filtered set, with its actual vs expected resolution time.</summary>
public record ResolutionTimeReportRow(
    Guid TicketId, string ClientName, string? FailureTypeName, string? AssignedEmployeeName,
    DateTimeOffset? AssignedAt, DateTimeOffset? ResolvedAt, double? ResolutionHours,
    double? ExpectedResolutionHours, bool? WasOnTime
);

/// <summary>One row of the Customer-Rating report — one row per rated ticket (SatisfactionStars set) in the filtered set.</summary>
public record CustomerRatingReportRow(
    Guid TicketId, string ClientName, string? AssignedEmployeeName, DateTimeOffset? ResolvedAt,
    decimal SatisfactionStars, int SatisfactionScore, ClosureReason? ClosureReason
);

/// <summary>Generic paged+filtered result wrapper for a table report — same shape for all six, so the frontend has one table/pagination component to build against.</summary>
public record TableReportResult<T>(IReadOnlyList<T> Rows, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => TotalCount == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
