using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Application.Options;
using DaftechCrm.Domain.Entities;
using DaftechCrm.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DaftechCrm.Application.Services;

public class ReportService : IReportService
{
    private readonly IAppDbContext _db;
    private readonly TicketWorkflowOptions _options;
    private readonly IAiNarrativeReportService _aiNarrative;

    public ReportService(IAppDbContext db, IOptions<TicketWorkflowOptions> options, IAiNarrativeReportService aiNarrative)
    {
        _db = db;
        _options = options.Value;
        _aiNarrative = aiNarrative;
    }

    /// <summary>
    /// "On time" = ResolvedAt - AssignedAt is within the ticket's own
    /// frozen SLA target (Ticket.ExpectedResolutionMinutes, snapshotted at
    /// assignment time — see TicketService.SubmitFromClientAsync) if one
    /// was recorded, otherwise the global OnTimeResolutionTargetDays
    /// fallback. Only tickets that have both AssignedAt and ResolvedAt set
    /// are counted (i.e. tickets that actually reached Resolved at some
    /// point) — tickets still in progress or never assigned don't factor
    /// in yet.
    ///
    /// Deliberately reads Ticket.ExpectedResolutionMinutes, NOT
    /// t.FailureType.ToTimeSpan() — the latter is the FailureType's
    /// CURRENT configured duration, which an Admin can edit at any time.
    /// Reading it live here would silently reclassify already-resolved
    /// tickets as on-time/late every time this report runs, based on
    /// today's settings rather than what applied when the ticket was
    /// actually worked. Tickets resolved before this snapshot field
    /// existed have ExpectedResolutionMinutes = null and fall back to the
    /// global target, same as a ticket with no FailureType at all.
    /// </summary>
    public async Task<OnTimeReportDto> GetOnTimeResolutionReportAsync(CancellationToken ct = default)
    {
        var fallbackSpan = TimeSpan.FromDays(_options.OnTimeResolutionTargetDays);

        var resolvedTickets = await _db.Tickets
            .AsNoTracking()
            .Include(t => t.AssignedEmployee)
            .Where(t => t.AssignedAt != null && t.ResolvedAt != null)
            .ToListAsync(ct);

        TimeSpan TargetFor(Ticket t) => t.ExpectedResolutionMinutes is int mins ? TimeSpan.FromMinutes(mins) : fallbackSpan;
        bool IsOnTime(Ticket t) => (t.ResolvedAt!.Value - t.AssignedAt!.Value) <= TargetFor(t);

        var onTime = resolvedTickets.Count(IsOnTime);
        var late = resolvedTickets.Count - onTime;
        var overallRate = resolvedTickets.Count > 0 ? Math.Round(onTime * 100.0 / resolvedTickets.Count, 1) : 0;

        var summary = new OnTimeSummaryDto(onTime, late, resolvedTickets.Count, overallRate, _options.OnTimeResolutionTargetDays);

        var byEmployee = resolvedTickets
            .Where(t => t.AssignedEmployeeId != null)
            .GroupBy(t => new { t.AssignedEmployeeId, Name = t.AssignedEmployee?.FullName ?? "Unknown" })
            .Select(g =>
            {
                var onTimeCount = g.Count(IsOnTime);
                var total = g.Count();
                return new EmployeeOnTimeStatsDto(
                    g.Key.AssignedEmployeeId!.Value,
                    g.Key.Name,
                    onTimeCount,
                    total - onTimeCount,
                    total,
                    total > 0 ? Math.Round(onTimeCount * 100.0 / total, 1) : 0
                );
            })
            .OrderByDescending(e => e.OnTimeRate)
            .ToList();

        return new OnTimeReportDto(summary, byEmployee);
    }

    public async Task<EmployeePerformanceReportDto> GetEmployeePerformanceReportAsync(Guid employeeId, bool includeAiNarrative, CancellationToken ct = default)
    {
        var employee = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId, ct)
            ?? throw new InvalidOperationException("Employee not found.");

        var assignedTickets = await _db.Tickets.AsNoTracking().Where(t => t.AssignedEmployeeId == employeeId).ToListAsync(ct);
        var resolvedOrClosed = assignedTickets.Where(t => t.ResolvedAt != null).ToList();

        double? avgResolutionHours = null;
        var withBothTimestamps = assignedTickets.Where(t => t.AssignedAt != null && t.ResolvedAt != null).ToList();
        if (withBothTimestamps.Count > 0)
            avgResolutionHours = withBothTimestamps.Average(t => (t.ResolvedAt!.Value - t.AssignedAt!.Value).TotalHours);

        // Same reasoning as GetOnTimeResolutionReportAsync above: read the
        // frozen per-ticket snapshot, not FailureType's current duration,
        // so editing a FailureType later doesn't retroactively change this
        // employee's historical on-time rate.
        var fallbackSpan = TimeSpan.FromDays(_options.OnTimeResolutionTargetDays);
        TimeSpan TargetFor(Ticket t) => t.ExpectedResolutionMinutes is int mins ? TimeSpan.FromMinutes(mins) : fallbackSpan;
        var onTimeCount = withBothTimestamps.Count(t => (t.ResolvedAt!.Value - t.AssignedAt!.Value) <= TargetFor(t));
        var onTimeRate = withBothTimestamps.Count > 0 ? Math.Round(onTimeCount * 100.0 / withBothTimestamps.Count, 1) : 0;

        var scores = assignedTickets.Where(t => t.SatisfactionScore != null).Select(t => t.SatisfactionScore!.Value).ToList();
        double? avgSatisfaction = scores.Count > 0 ? scores.Average() : null;

        var totalHours = await _db.TimeLogs
            .Where(l => l.EmployeeId == employeeId && l.TotalHours != null)
            .SumAsync(l => l.TotalHours!.Value, ct);

        bool aiAvailable = false;
        string? narrative = null;
        string? unavailableReason = includeAiNarrative ? null : "AI narrative not requested.";

        if (includeAiNarrative)
        {
            var metrics = new EmployeePerformanceMetrics(
                employee.FullName, assignedTickets.Count, resolvedOrClosed.Count,
                avgResolutionHours, onTimeRate, avgSatisfaction, totalHours
            );
            var aiResult = await _aiNarrative.SummarizeEmployeePerformanceAsync(metrics, ct);
            aiAvailable = aiResult.Available;
            narrative = aiResult.Narrative;
            unavailableReason = aiResult.UnavailableReason;
        }

        return new EmployeePerformanceReportDto(
            employee.Id, employee.FullName, assignedTickets.Count, resolvedOrClosed.Count,
            avgResolutionHours, onTimeRate, avgSatisfaction, totalHours,
            aiAvailable, narrative, unavailableReason
        );
    }

    public async Task<AiPerformanceSummaryResult> SummarizeTabularReportAsync(TabularReportData data, CancellationToken ct = default)
    {
        if (data.Rows.Count == 0)
            return new AiPerformanceSummaryResult(false, null, "No data to summarize yet.");

        return await _aiNarrative.SummarizeTabularReportAsync(data, ct);
    }

    /// <summary>
    /// Every ticket in the system grouped by its current status — a
    /// live snapshot of "what's actually going on right now" for the
    /// admin's overall-operations pie chart, as opposed to
    /// GetOnTimeResolutionReportAsync above, which only looks backward
    /// at tickets that have already reached Resolved.
    /// </summary>
    public async Task<OperationsOverviewDto> GetOperationsOverviewAsync(CancellationToken ct = default)
    {
        var statusCounts = await _db.Tickets
            .AsNoTracking()
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        // Every TicketStatus value is represented, even at zero, so the
        // pie chart's legend is stable across refreshes instead of
        // slices appearing/disappearing as the queue empties out.
        var byStatus = Enum.GetValues<TicketStatus>()
            .Select(status => new TicketStatusSliceDto(
                status.ToString(),
                statusCounts.FirstOrDefault(s => s.Status == status)?.Count ?? 0))
            .ToList();

        var totalTickets = byStatus.Sum(s => s.Count);

        var activeClients = await _db.Clients.CountAsync(c => c.AccountStatus == ClientAccountStatus.Approved, ct);
        var activeEmployees = await _db.Employees.CountAsync(e => e.AccountStatus == EmployeeAccountStatus.Active, ct);
        var openAgreements = await _db.Agreements.CountAsync(a => a.Status == AgreementStatus.Active, ct);

        return new OperationsOverviewDto(byStatus, totalTickets, activeClients, activeEmployees, openAgreements);
    }
}
