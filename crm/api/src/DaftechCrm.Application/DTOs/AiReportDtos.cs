namespace DaftechCrm.Application.DTOs;

/// <summary>Input metrics handed to the AI summarizer — same numbers already shown in written/graphical reports, never the only source of truth.</summary>
public record EmployeePerformanceMetrics(
    string EmployeeName,
    int TicketsAssigned,
    int TicketsResolved,
    double? AverageResolutionHours,
    double OnTimeRate,
    double? AverageSatisfactionScore,
    double TotalHoursWorked
);

public record AiPerformanceSummaryResult(bool Available, string? Narrative, string? UnavailableReason);

/// <summary>
/// A generic tabular report handed to the AI summarizer for the Reports
/// page — same columns/rows already rendered on screen and in the PDF
/// export, never a separate source of truth. Title/columns/rows only;
/// the summarizer narrates what's already there, it doesn't compute
/// anything new.
/// </summary>
public record TabularReportData(string Title, IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string>> Rows);
