using DaftechCrm.Domain.Entities;
using DaftechCrm.Domain.Enums;

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
    double TotalHoursWorked
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
    SupportOverviewDto SupportOverview,
    /// <summary>
    /// Names of dashboard sections that could not be computed on this
    /// request (e.g. "supportOverview", "ticketsByFailureType"). The
    /// endpoint returns 200 with the sections that DID succeed rather than
    /// failing the whole page with a 500 — the Dashboard renders a
    /// per-widget notice for anything listed here. Empty on a healthy load.
    /// </summary>
    IReadOnlyList<string>? FailedSections = null
);

/// <summary>
/// Filters the Dashboard's charts/KPIs can be scoped by — a small subset
/// of TicketReportFilter's dimensions (date range + region), since the
/// Dashboard is a live at-a-glance view, not the deep per-dimension drill
/// -down the Reports module's six tables provide.
/// </summary>
public record DashboardFilter(DateOnly? FromDate, DateOnly? ToDate, string? Region);

// --- Overall Client Report (admin Reports page — one client's full history) ---

/// <summary>One ticket's file references, folded into ClientReportTicketDto below rather than left to a separate lookup — the report is meant to be a single self-contained snapshot an Admin can hand off or print.</summary>
public record ClientReportTicketDto(
    Guid Id,
    string Description,
    TicketCategory Category,
    string? FailureTypeName,
    DateTimeOffset DateSubmitted,
    string? AssignedEmployeeName,
    TicketStatus Status,
    SupportPhase SupportPhase,
    bool Chargeable,
    decimal? ChargeAmount,
    DateTimeOffset? ResolvedAt,
    decimal? SatisfactionStars,
    int? SatisfactionScore,
    ClosureReason? ClosureReason,
    /// <summary>Original filename of the ticket's attachment (screenshot/document), or null if none was uploaded. Listed for a complete record — fetching the file itself still goes through the ticket's own /attachment endpoint.</summary>
    string? AttachmentFileName,
    /// <summary>Original filename of the ticket's voice-note recording, or null if none was recorded.</summary>
    string? VoiceNoteFileName
);

/// <summary>One agreement under one of the client's systems/products, folded into ClientReportSystemProductDto.</summary>
public record ClientReportAgreementDto(
    Guid Id,
    string AgreementTypeName,
    string DocumentNumber,
    DateOnly SignDate,
    DateOnly ExpiryDate,
    int SupportWindowMonths,
    AgreementStatus Status,
    BillingTier BillingTier
);

/// <summary>One training session logged against one of the client's systems/products.</summary>
public record ClientReportTrainingRecordDto(
    Guid Id,
    string TrainerEmployeeName,
    DateOnly TrainingDate,
    string Description,
    string? FileName
);

/// <summary>One system/product the client has, with everything hanging off it — agreements and the training log — gathered in one place instead of three separate lookups the reader has to cross-reference by SystemProductId themselves.</summary>
public record ClientReportSystemProductDto(
    Guid Id,
    string ReferenceNumber,
    string Name,
    string? Description,
    DateOnly? DeploymentDate,
    TrainingCompletionStatus TrainingCompletionStatus,
    IReadOnlyList<ClientReportAgreementDto> Agreements,
    IReadOnlyList<ClientReportTrainingRecordDto> TrainingRecords
);

/// <summary>One rating the client gave to one admin-authored question, snapshotted at submission time.</summary>
public record ClientReportSurveyAnswerDto(string QuestionText, int Rating);

/// <summary>One submitted satisfaction survey (admin-configurable questions) — separate from Ticket.SatisfactionStars, same as everywhere else this survey is surfaced.</summary>
public record ClientReportSurveyDto(
    Guid Id,
    Guid TicketId,
    DateTimeOffset SubmittedAt,
    IReadOnlyList<ClientReportSurveyAnswerDto> Answers,
    string? SatisfactionComment
);

/// <summary>
/// Headline counts for the report's summary block — computed once here so
/// the frontend (and the printed/PDF output) don't each have to
/// re-derive the same numbers from the raw lists above.
/// </summary>
public record ClientReportSummaryDto(
    int SystemProductCount,
    int ActiveAgreementCount,
    int TotalTicketCount,
    int OpenTicketCount,
    int ResolvedTicketCount,
    double? AverageSatisfactionScore,
    int SurveyCount
);

/// <summary>
/// Everything about one client in a single call, for the admin Reports
/// page's "Overall Client Report" tab: profile, every system/product with
/// its agreements and training history, every ticket (including which
/// ones carry an attachment/voice-note), every satisfaction survey, and a
/// summary block. Meant to be printed or exported to PDF as a standalone
/// document — see ReportsController.GetOverallClientReport.
/// </summary>
public record OverallClientReportDto(
    Guid ClientId,
    string ClientName,
    string AccountRefId,
    string PhoneNumber,
    string Email,
    string Office,
    string Location,
    string? Region,
    string? Zone,
    string? City,
    string? Woreda,
    ClientAccountStatus AccountStatus,
    DateOnly OnboardingDate,
    IReadOnlyList<ClientReportSystemProductDto> SystemProducts,
    IReadOnlyList<ClientReportTicketDto> Tickets,
    IReadOnlyList<ClientReportSurveyDto> SatisfactionSurveys,
    ClientReportSummaryDto Summary
);
