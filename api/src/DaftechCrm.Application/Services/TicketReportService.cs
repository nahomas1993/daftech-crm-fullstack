using System.Text;
using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Application.Options;
using DaftechCrm.Domain.Entities;
using DaftechCrm.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DaftechCrm.Application.Services;

/// <summary>
/// Backs the Reports module — six table-only reports, each filterable,
/// searchable, paginated, and exportable (PDF via QuestPDF, CSV directly).
/// Deliberately does not touch chart/KPI logic — see ReportService for
/// that (Dashboard-only, per the product's Reports-vs-Dashboard split).
///
/// Resolution-time figures throughout use WORKING minutes (see
/// IEthiopianTimeService), not wall-clock elapsed time — a ticket assigned
/// Friday 11:00 and resolved Monday 3:00 shows a few working hours here,
/// not the ~64 wall-clock hours that elapsed including the weekend/lunch
/// pauses. This matches how the resolution timer itself pauses (see
/// TicketService), so a report's numbers agree with what a technician
/// actually experienced.
/// </summary>
public class TicketReportService : ITicketReportService
{
    private readonly IAppDbContext _db;
    private readonly TicketWorkflowOptions _options;
    private readonly IEthiopianTimeService _officeTime;

    public TicketReportService(IAppDbContext db, IOptions<TicketWorkflowOptions> options, IEthiopianTimeService officeTime)
    {
        _db = db;
        _options = options.Value;
        _officeTime = officeTime;
    }

    /// <summary>
    /// The single shared filter pipeline every report starts from — a
    /// left-joined Ticket/Client/FailureType/AssignedEmployee/SystemProduct
    /// projection with every TicketReportFilter dimension applied. Kept as
    /// one method so the six reports can never drift into filtering
    /// slightly differently from one another.
    /// </summary>
    private IQueryable<Ticket> FilteredTickets(TicketReportFilter filter)
    {
        var query = _db.Tickets.AsNoTracking()
            .Include(t => t.Client)
            .Include(t => t.FailureType)
            .Include(t => t.AssignedEmployee)
            .Include(t => t.Agreement).ThenInclude(a => a.SystemProduct)
            .AsQueryable();

        if (filter.FromDate.HasValue)
        {
            var from = filter.FromDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(t => t.DateSubmitted >= from);
        }
        if (filter.ToDate.HasValue)
        {
            // Inclusive of the whole ToDate day.
            var to = filter.ToDate.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
            query = query.Where(t => t.DateSubmitted <= to);
        }
        if (filter.Month.HasValue)
            query = query.Where(t => t.DateSubmitted.Month == filter.Month.Value);
        if (!string.IsNullOrWhiteSpace(filter.Region))
            query = query.Where(t => t.Client.Region == filter.Region);
        if (!string.IsNullOrWhiteSpace(filter.Zone))
            query = query.Where(t => t.Client.Zone == filter.Zone);
        if (!string.IsNullOrWhiteSpace(filter.Woreda))
            query = query.Where(t => t.Client.Woreda == filter.Woreda);
        if (filter.EmployeeId.HasValue)
            query = query.Where(t => t.AssignedEmployeeId == filter.EmployeeId.Value);
        if (filter.FailureTypeId.HasValue)
            query = query.Where(t => t.FailureTypeId == filter.FailureTypeId.Value);
        if (filter.Status.HasValue)
            query = query.Where(t => t.Status == filter.Status.Value);
        // SupportPhase is computed client-side (not a mapped column — see
        // Ticket.SupportPhase), so it can't be pushed into SQL. Translate
        // it to the equivalent set of underlying Status values instead,
        // which IS translatable.
        if (filter.SupportPhase.HasValue)
        {
            var statuses = StatusesForPhase(filter.SupportPhase.Value);
            query = query.Where(t => statuses.Contains(t.Status));
        }
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            query = query.Where(t =>
                t.Description.Contains(term) ||
                t.Client.Name.Contains(term) ||
                t.Agreement.DocumentNumber.Contains(term));
        }

        return query;
    }

    private static TicketStatus[] StatusesForPhase(SupportPhase phase) => phase switch
    {
        SupportPhase.Intake => new[] { TicketStatus.Submitted, TicketStatus.Forwarded },
        SupportPhase.Diagnosis => new[] { TicketStatus.Assigned },
        SupportPhase.Repair => new[] { TicketStatus.InProgress },
        SupportPhase.Verification => new[] { TicketStatus.Resolved, TicketStatus.AwaitingClientConfirmation },
        SupportPhase.Closed => new[] { TicketStatus.Escalated, TicketStatus.Closed },
        _ => Array.Empty<TicketStatus>(),
    };

    private TimeSpan ExpectedResolutionFor(Ticket t) =>
        t.ExpectedResolutionMinutes is int mins ? TimeSpan.FromMinutes(mins) : TimeSpan.FromDays(_options.OnTimeResolutionTargetDays);

    /// <summary>Working hours (not wall-clock) between AssignedAt and ResolvedAt — null if either is unset. See class remarks for why working hours, not wall-clock.</summary>
    private double? WorkingResolutionHours(Ticket t) =>
        t.AssignedAt is DateTimeOffset assigned && t.ResolvedAt is DateTimeOffset resolved
            ? _officeTime.WorkingMinutesElapsed(assigned, resolved) / 60.0
            : null;

    private bool IsOnTime(Ticket t) =>
        t.AssignedAt is DateTimeOffset assigned && t.ResolvedAt is DateTimeOffset resolved &&
        _officeTime.WorkingMinutesElapsed(assigned, resolved) <= ExpectedResolutionFor(t).TotalMinutes;

    // --- Report 1: Customer/Support ---

    public async Task<TableReportResult<CustomerSupportReportRow>> GetCustomerSupportReportAsync(TicketReportFilter filter, PaginationQuery paging, CancellationToken ct = default)
    {
        var query = FilteredTickets(filter).OrderByDescending(t => t.DateSubmitted);
        var totalCount = await query.CountAsync(ct);
        var tickets = await query.Skip(paging.Skip).Take(paging.PageSize).ToListAsync(ct);

        var rows = tickets.Select(t => new CustomerSupportReportRow(
            t.Id, t.Client?.Name ?? "Unknown client", t.Client?.Region, t.Client?.Zone, t.Client?.Woreda,
            t.Agreement?.SystemProduct?.Name ?? "Unspecified", t.FailureType?.Name, t.DateSubmitted,
            t.AssignedEmployee?.FullName, t.Status, t.SupportPhase,
            t.Chargeable, t.ResolvedAt, t.SatisfactionScore
        )).ToList();

        return new TableReportResult<CustomerSupportReportRow>(rows, paging.Page, paging.PageSize, totalCount);
    }

    // --- Report 2: Employee Performance ---

    public async Task<TableReportResult<EmployeePerformanceReportRow>> GetEmployeePerformanceReportAsync(TicketReportFilter filter, PaginationQuery paging, CancellationToken ct = default)
    {
        var tickets = await FilteredTickets(filter).Where(t => t.AssignedEmployeeId != null).ToListAsync(ct);

        var now = DateTimeOffset.UtcNow;
        var allRows = tickets
            .GroupBy(t => new { t.AssignedEmployeeId, Name = t.AssignedEmployee?.FullName ?? "Unknown employee" })
            .Select(g =>
            {
                var withBoth = g.Where(t => t.AssignedAt != null && t.ResolvedAt != null).ToList();
                var resolutionHours = withBoth.Count > 0 ? withBoth.Average(t => WorkingResolutionHours(t)!.Value) : (double?)null;
                var onTimeCount = withBoth.Count(IsOnTime);
                var onTimeRate = withBoth.Count > 0 ? Math.Round(onTimeCount * 100.0 / withBoth.Count, 1) : (double?)null;
                var scores = g.Where(t => t.SatisfactionScore != null).Select(t => t.SatisfactionScore!.Value).ToList();
                var openCount = g.Count(t => t.Status is TicketStatus.Assigned or TicketStatus.InProgress or TicketStatus.AwaitingClientConfirmation);
                var overdueCount = g.Count(t => t.ResolvedAt == null && t.ExpectedResolutionBy != null && t.ExpectedResolutionBy < now);

                return new EmployeePerformanceReportRow(
                    g.Key.AssignedEmployeeId!.Value, g.Key.Name, g.Count(),
                    g.Count(t => t.ResolvedAt != null), openCount, overdueCount,
                    resolutionHours, onTimeRate, scores.Count > 0 ? scores.Average() : (double?)null
                );
            })
            .OrderByDescending(r => r.OnTimeRatePercent ?? 0)
            .ToList();

        var page = allRows.Skip(paging.Skip).Take(paging.PageSize).ToList();
        return new TableReportResult<EmployeePerformanceReportRow>(page, paging.Page, paging.PageSize, allRows.Count);
    }

    // --- Report 3: Regional ---

    public async Task<TableReportResult<RegionalReportRow>> GetRegionalReportAsync(TicketReportFilter filter, PaginationQuery paging, CancellationToken ct = default)
    {
        var tickets = await FilteredTickets(filter).ToListAsync(ct);

        var allRows = tickets
            .GroupBy(t => new { Region = t.Client?.Region, Zone = t.Client?.Zone, Woreda = t.Client?.Woreda })
            .Select(g =>
            {
                var withBoth = g.Where(t => t.AssignedAt != null && t.ResolvedAt != null).ToList();
                var resolutionHours = withBoth.Count > 0 ? withBoth.Average(t => WorkingResolutionHours(t)!.Value) : (double?)null;
                var scores = g.Where(t => t.SatisfactionScore != null).Select(t => t.SatisfactionScore!.Value).ToList();
                var openCount = g.Count(t => t.Status is not (TicketStatus.Closed or TicketStatus.Escalated));

                return new RegionalReportRow(
                    g.Key.Region, g.Key.Zone, g.Key.Woreda, g.Count(), openCount,
                    g.Count(t => t.ResolvedAt != null), resolutionHours,
                    scores.Count > 0 ? scores.Average() : (double?)null
                );
            })
            .OrderByDescending(r => r.TicketCount)
            .ToList();

        var page = allRows.Skip(paging.Skip).Take(paging.PageSize).ToList();
        return new TableReportResult<RegionalReportRow>(page, paging.Page, paging.PageSize, allRows.Count);
    }

    // --- Report 4: Failure Type ---

    public async Task<TableReportResult<FailureTypeReportRow>> GetFailureTypeReportAsync(TicketReportFilter filter, PaginationQuery paging, CancellationToken ct = default)
    {
        var tickets = await FilteredTickets(filter).ToListAsync(ct);

        var allRows = tickets
            .GroupBy(t => new { t.FailureTypeId, Name = t.FailureType?.Name ?? "Unspecified" })
            .Select(g =>
            {
                var withBoth = g.Where(t => t.AssignedAt != null && t.ResolvedAt != null).ToList();
                var onTimeCount = withBoth.Count(IsOnTime);
                var lateCount = withBoth.Count - onTimeCount;
                var resolutionHours = withBoth.Count > 0 ? withBoth.Average(t => WorkingResolutionHours(t)!.Value) : (double?)null;

                return new FailureTypeReportRow(
                    g.Key.FailureTypeId, g.Key.Name, g.Count(), onTimeCount, lateCount,
                    withBoth.Count > 0 ? Math.Round(onTimeCount * 100.0 / withBoth.Count, 1) : (double?)null,
                    resolutionHours
                );
            })
            .OrderByDescending(r => r.TicketCount)
            .ToList();

        var page = allRows.Skip(paging.Skip).Take(paging.PageSize).ToList();
        return new TableReportResult<FailureTypeReportRow>(page, paging.Page, paging.PageSize, allRows.Count);
    }

    // --- Report 5: Resolution Time ---

    public async Task<TableReportResult<ResolutionTimeReportRow>> GetResolutionTimeReportAsync(TicketReportFilter filter, PaginationQuery paging, CancellationToken ct = default)
    {
        var query = FilteredTickets(filter).Where(t => t.ResolvedAt != null).OrderByDescending(t => t.ResolvedAt);
        var totalCount = await query.CountAsync(ct);
        var tickets = await query.Skip(paging.Skip).Take(paging.PageSize).ToListAsync(ct);

        var rows = tickets.Select(t =>
        {
            double? resolutionHours = WorkingResolutionHours(t);
            var expected = ExpectedResolutionFor(t);
            bool? onTime = t.AssignedAt != null ? IsOnTime(t) : null;

            return new ResolutionTimeReportRow(
                t.Id, t.Client?.Name ?? "Unknown client", t.FailureType?.Name, t.AssignedEmployee?.FullName,
                t.AssignedAt, t.ResolvedAt, resolutionHours, expected.TotalHours, onTime
            );
        }).ToList();

        return new TableReportResult<ResolutionTimeReportRow>(rows, paging.Page, paging.PageSize, totalCount);
    }

    // --- Report 6: Customer Rating ---

    public async Task<TableReportResult<CustomerRatingReportRow>> GetCustomerRatingReportAsync(TicketReportFilter filter, PaginationQuery paging, CancellationToken ct = default)
    {
        var query = FilteredTickets(filter).Where(t => t.SatisfactionStars != null).OrderByDescending(t => t.ResolvedAt);
        var totalCount = await query.CountAsync(ct);
        var tickets = await query.Skip(paging.Skip).Take(paging.PageSize).ToListAsync(ct);

        var rows = tickets.Select(t => new CustomerRatingReportRow(
            t.Id, t.Client?.Name ?? "Unknown client", t.AssignedEmployee?.FullName, t.ResolvedAt,
            t.SatisfactionStars!.Value, t.SatisfactionScore!.Value, t.ClosureReason
        )).ToList();

        return new TableReportResult<CustomerRatingReportRow>(rows, paging.Page, paging.PageSize, totalCount);
    }

    // --- Export ---

    /// <summary>Fetches every filtered row (unpaged) for a given report type — the shared source for both PDF and CSV export, since an exported report should always contain the full filtered result, not just the current page.</summary>
    private async Task<(string Title, string[] Headers, IReadOnlyList<string[]> Rows)> BuildExportDataAsync(string reportType, TicketReportFilter filter, CancellationToken ct)
    {
        var unpaged = new PaginationQuery { Page = 1, PageSize = 10_000 };

        switch (reportType.Trim().ToLowerInvariant())
        {
            case "customer-support":
                var cs = await GetCustomerSupportReportAsync(filter, unpaged, ct);
                return ("Customer / Support Report",
                    new[] { "Client", "Region", "Zone", "Woreda", "System/Product", "Failure Type", "Submitted", "Assigned To", "Status", "Phase", "Chargeable", "Resolved", "Satisfaction" },
                    cs.Rows.Select(r => new[] {
                        r.ClientName, r.Region ?? "", r.Zone ?? "", r.Woreda ?? "", r.SystemProductName ?? "", r.FailureTypeName ?? "",
                        r.DateSubmitted.ToString("yyyy-MM-dd"), r.AssignedEmployeeName ?? "Unassigned", r.Status.ToString(), r.SupportPhase.ToString(),
                        r.Chargeable ? "Yes" : "No", r.ResolvedAt?.ToString("yyyy-MM-dd") ?? "", r.SatisfactionScore?.ToString() ?? "",
                    }).ToList());

            case "employee-performance":
                var ep = await GetEmployeePerformanceReportAsync(filter, unpaged, ct);
                return ("Employee Performance Report",
                    new[] { "Employee", "Total Assigned", "Resolved", "Open", "Overdue", "Avg Resolution (hrs)", "On-Time %", "Avg Satisfaction" },
                    ep.Rows.Select(r => new[] {
                        r.EmployeeName, r.TotalAssigned.ToString(), r.Resolved.ToString(), r.Open.ToString(), r.Overdue.ToString(),
                        r.AverageResolutionHours?.ToString("F1") ?? "", r.OnTimeRatePercent?.ToString("F1") ?? "", r.AverageSatisfactionScore?.ToString("F1") ?? "",
                    }).ToList());

            case "regional":
                var rg = await GetRegionalReportAsync(filter, unpaged, ct);
                return ("Regional Report",
                    new[] { "Region", "Zone", "Woreda", "Tickets", "Open", "Resolved", "Avg Resolution (hrs)", "Avg Satisfaction" },
                    rg.Rows.Select(r => new[] {
                        r.Region ?? "Unspecified", r.Zone ?? "", r.Woreda ?? "", r.TicketCount.ToString(), r.OpenCount.ToString(), r.ResolvedCount.ToString(),
                        r.AverageResolutionHours?.ToString("F1") ?? "", r.AverageSatisfactionScore?.ToString("F1") ?? "",
                    }).ToList());

            case "failure-type":
                var ft = await GetFailureTypeReportAsync(filter, unpaged, ct);
                return ("Failure-Type Report",
                    new[] { "Failure Type", "Tickets", "On-Time", "Late", "On-Time %", "Avg Resolution (hrs)" },
                    ft.Rows.Select(r => new[] {
                        r.FailureTypeName, r.TicketCount.ToString(), r.OnTimeCount.ToString(), r.LateCount.ToString(),
                        r.OnTimeRatePercent?.ToString("F1") ?? "", r.AverageResolutionHours?.ToString("F1") ?? "",
                    }).ToList());

            case "resolution-time":
                var rt = await GetResolutionTimeReportAsync(filter, unpaged, ct);
                return ("Resolution-Time Report",
                    new[] { "Client", "Failure Type", "Assigned To", "Assigned At", "Resolved At", "Resolution (hrs)", "Expected (hrs)", "On Time" },
                    rt.Rows.Select(r => new[] {
                        r.ClientName, r.FailureTypeName ?? "", r.AssignedEmployeeName ?? "",
                        r.AssignedAt?.ToString("yyyy-MM-dd HH:mm") ?? "", r.ResolvedAt?.ToString("yyyy-MM-dd HH:mm") ?? "",
                        r.ResolutionHours?.ToString("F1") ?? "", r.ExpectedResolutionHours?.ToString("F1") ?? "", r.WasOnTime is bool b ? (b ? "Yes" : "No") : "",
                    }).ToList());

            case "customer-rating":
                var cr = await GetCustomerRatingReportAsync(filter, unpaged, ct);
                return ("Customer-Rating Report",
                    new[] { "Client", "Assigned To", "Resolved At", "Stars", "Score", "Closure Reason" },
                    cr.Rows.Select(r => new[] {
                        r.ClientName, r.AssignedEmployeeName ?? "", r.ResolvedAt?.ToString("yyyy-MM-dd") ?? "",
                        r.SatisfactionStars.ToString(), r.SatisfactionScore.ToString(), r.ClosureReason?.ToString() ?? "",
                    }).ToList());

            default:
                throw new InvalidOperationException($"Unknown report type: {reportType}");
        }
    }

    public async Task<string> ExportCsvAsync(string reportType, TicketReportFilter filter, CancellationToken ct = default)
    {
        var (_, headers, rows) = await BuildExportDataAsync(reportType, filter, ct);

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", headers.Select(CsvEscape)));
        foreach (var row in rows)
            sb.AppendLine(string.Join(",", row.Select(CsvEscape)));

        return sb.ToString();
    }

    private static string CsvEscape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }

    public async Task<byte[]> ExportPdfAsync(string reportType, TicketReportFilter filter, CancellationToken ct = default)
    {
        var (title, headers, rows) = await BuildExportDataAsync(reportType, filter, ct);
        return TabularPdfRenderer.Render(title, headers, rows);
    }
}
