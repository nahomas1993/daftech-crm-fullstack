using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Application.Options;
using DaftechCrm.Domain.Entities;
using DaftechCrm.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Caching.Memory;

namespace DaftechCrm.Application.Services;

public class ReportService : IReportService
{
    private readonly IAppDbContext _db;
    private readonly TicketWorkflowOptions _options;
    private readonly IEthiopianTimeService _officeTime;
    private readonly IMemoryCache _cache;

    public ReportService(IAppDbContext db, IOptions<TicketWorkflowOptions> options, IEthiopianTimeService officeTime, IMemoryCache cache)
    {
        _db = db;
        _options = options.Value;
        _officeTime = officeTime;
        _cache = cache;
    }

    /// <summary>
    /// "On time" = working hours (see IEthiopianTimeService) from
    /// AssignedAt to ResolvedAt are within the ticket's own frozen SLA
    /// target (Ticket.ExpectedResolutionMinutes, snapshotted at assignment
    /// time — see TicketService.SubmitFromClientAsync) if one was
    /// recorded, otherwise the global OnTimeResolutionTargetDays fallback.
    /// Working hours, not wall-clock, so a ticket assigned Friday and
    /// resolved Monday isn't penalized for the weekend/lunch time in
    /// between — matches how the resolution timer itself pauses (see
    /// TicketService). Only tickets that have both AssignedAt and
    /// ResolvedAt set are counted (i.e. tickets that actually reached
    /// Resolved at some point) — tickets still in progress or never
    /// assigned don't factor in yet.
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
        bool IsOnTime(Ticket t) => _officeTime.WorkingMinutesElapsed(t.AssignedAt!.Value, t.ResolvedAt!.Value) <= TargetFor(t).TotalMinutes;

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

    public async Task<EmployeePerformanceReportDto> GetEmployeePerformanceReportAsync(Guid employeeId, CancellationToken ct = default)
    {
        var employee = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == employeeId, ct)
            ?? throw new InvalidOperationException("Employee not found.");

        var assignedTickets = await _db.Tickets.AsNoTracking().Where(t => t.AssignedEmployeeId == employeeId).ToListAsync(ct);
        var resolvedOrClosed = assignedTickets.Where(t => t.ResolvedAt != null).ToList();

        double? avgResolutionHours = null;
        var withBothTimestamps = assignedTickets.Where(t => t.AssignedAt != null && t.ResolvedAt != null).ToList();
        if (withBothTimestamps.Count > 0)
            avgResolutionHours = withBothTimestamps.Average(t => _officeTime.WorkingMinutesElapsed(t.AssignedAt!.Value, t.ResolvedAt!.Value) / 60.0);

        // Same reasoning as GetOnTimeResolutionReportAsync above: read the
        // frozen per-ticket snapshot, not FailureType's current duration,
        // so editing a FailureType later doesn't retroactively change this
        // employee's historical on-time rate. Working hours, not
        // wall-clock — same as GetOnTimeResolutionReportAsync.
        var fallbackSpan = TimeSpan.FromDays(_options.OnTimeResolutionTargetDays);
        TimeSpan TargetFor(Ticket t) => t.ExpectedResolutionMinutes is int mins ? TimeSpan.FromMinutes(mins) : fallbackSpan;
        var onTimeCount = withBothTimestamps.Count(t => _officeTime.WorkingMinutesElapsed(t.AssignedAt!.Value, t.ResolvedAt!.Value) <= TargetFor(t).TotalMinutes);
        var onTimeRate = withBothTimestamps.Count > 0 ? Math.Round(onTimeCount * 100.0 / withBothTimestamps.Count, 1) : 0;

        var scores = assignedTickets.Where(t => t.SatisfactionScore != null).Select(t => t.SatisfactionScore!.Value).ToList();
        double? avgSatisfaction = scores.Count > 0 ? scores.Average() : null;

        var totalHours = await _db.TimeLogs
            .Where(l => l.EmployeeId == employeeId && l.TotalHours != null)
            .SumAsync(l => l.TotalHours!.Value, ct);

        return new EmployeePerformanceReportDto(
            employee.Id, employee.FullName, assignedTickets.Count, resolvedOrClosed.Count,
            avgResolutionHours, onTimeRate, avgSatisfaction, totalHours
        );
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

    public async Task<SupportOverviewDto> GetSupportOverviewAsync(CancellationToken ct = default)
    {
        const string cacheKey = "reports:support-overview";
        if (_cache.TryGetValue(cacheKey, out SupportOverviewDto? cached) && cached is not null)
            return cached;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var horizon = today.AddDays(30);

        // Projected through nullable locals rather than straight into
        // ExpiringClientDto: EF renders the SystemProduct/Client navigations
        // as LEFT JOINs, so an agreement whose SystemProduct (or that
        // product's Client) row is missing yields NULLs, and materializing
        // those into the DTO's non-nullable Guid/string members threw and
        // took the whole Dashboard down with a 500. Orphans are skipped.
        var expiring = await _db.Agreements.AsNoTracking()
            .Where(a => a.Status == AgreementStatus.Active && a.ExpiryDate >= today && a.ExpiryDate <= horizon)
            .Select(a => new
            {
                AgreementId = a.Id,
                ClientId = (Guid?)a.SystemProduct.ClientId,
                ClientName = (string?)a.SystemProduct.Client.Name,
                ProductName = (string?)a.SystemProduct.Name,
                a.ExpiryDate,
            })
            .ToListAsync(ct);

        var expiringWithDays = expiring
            .Where(x => x.ClientId != null)
            .Select(x => new ExpiringClientDto(
                x.ClientId!.Value,
                x.ClientName ?? "Unknown client",
                x.AgreementId,
                x.ProductName ?? "Unspecified",
                x.ExpiryDate,
                x.ExpiryDate.DayNumber - today.DayNumber))
            .OrderBy(x => x.DaysUntilExpiry)
            .ToList();

        // Same nullable-projection reasoning as `expiring` above: a ticket
        // pointing at a client row that no longer exists must not 500 the
        // whole overview.
        var freeGroups = await _db.Tickets.AsNoTracking().Where(t => !t.Chargeable)
            .GroupBy(t => new { t.ClientId, Name = (string?)t.Client.Name })
            .Select(g => new { g.Key.ClientId, g.Key.Name, Count = g.Count() })
            .ToListAsync(ct);
        var chargeableGroups = await _db.Tickets.AsNoTracking().Where(t => t.Chargeable)
            .GroupBy(t => new { t.ClientId, Name = (string?)t.Client.Name })
            .Select(g => new { g.Key.ClientId, g.Key.Name, Count = g.Count() })
            .ToListAsync(ct);

        var freeClients = freeGroups
            .Select(g => new SupportClientDto(g.ClientId, g.Name ?? "Unknown client", g.Count))
            .OrderByDescending(x => x.TicketCount).ToList();
        var chargeableClients = chargeableGroups
            .Select(g => new SupportClientDto(g.ClientId, g.Name ?? "Unknown client", g.Count))
            .OrderByDescending(x => x.TicketCount).ToList();

        var result = new SupportOverviewDto(
            expiringWithDays.Count,
            freeClients.Count,
            chargeableClients.Count,
            expiringWithDays,
            freeClients,
            chargeableClients);

        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
        return result;
    }

    /// <summary>See IReportService.GetDashboardDataAsync. Applies DashboardFilter once up front, then every chart/KPI below is computed from that same filtered ticket set — so a support manager filtering to "this month, Addis Ababa" sees every chart on the page agree with each other.</summary>
    public async Task<DashboardDataDto> GetDashboardDataAsync(DashboardFilter filter, CancellationToken ct = default)
    {
        var cacheKey = $"reports:dashboard:{filter.FromDate:yyyy-MM-dd}:{filter.ToDate:yyyy-MM-dd}:{filter.Region ?? "*"}";
        if (_cache.TryGetValue(cacheKey, out DashboardDataDto? cached) && cached is not null)
            return cached;

        var query = _db.Tickets.AsNoTracking()
            .Include(t => t.Client)
            .Include(t => t.FailureType)
            .Include(t => t.AssignedEmployee)
            .AsQueryable();

        if (filter.FromDate.HasValue)
            query = query.Where(t => t.DateSubmitted >= filter.FromDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        if (filter.ToDate.HasValue)
            query = query.Where(t => t.DateSubmitted <= filter.ToDate.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc));
        if (!string.IsNullOrWhiteSpace(filter.Region))
            query = query.Where(t => t.Client.Region == filter.Region);

        var tickets = await query.ToListAsync(ct);
        var now = DateTimeOffset.UtcNow;

        // Each chart/KPI block below is computed in isolation: one bad
        // section (a missing related row, a schema drift) degrades that one
        // widget instead of returning a 500 for the entire Dashboard, which
        // is what previously blanked the whole page.
        var failedSections = new List<string>();
        T Section<T>(string name, Func<T> compute, T fallback)
        {
            try { return compute(); }
            catch (Exception) { failedSections.Add(name); return fallback; }
        }

        // --- KPIs ---
        var kpis = Section("kpis", () =>
        {
            var openTickets = tickets.Count(t => t.Status is not (TicketStatus.Closed or TicketStatus.Escalated));
            var resolvedTickets = tickets.Count(t => t.ResolvedAt != null);
            var overdueTickets = tickets.Count(t => t.ResolvedAt == null && t.ExpectedResolutionBy != null && t.ExpectedResolutionBy < now);
            var withBoth = tickets.Where(t => t.AssignedAt != null && t.ResolvedAt != null).ToList();
            var onTimeCount = withBoth.Count(t =>
                _officeTime.WorkingMinutesElapsed(t.AssignedAt!.Value, t.ResolvedAt!.Value) <=
                (t.ExpectedResolutionMinutes is int mins ? mins : TimeSpan.FromDays(_options.OnTimeResolutionTargetDays).TotalMinutes));
            var resolutionRate = withBoth.Count > 0 ? Math.Round(onTimeCount * 100.0 / withBoth.Count, 1) : 0;
            var ratedScores = tickets.Where(t => t.SatisfactionScore != null).Select(t => t.SatisfactionScore!.Value).ToList();
            var avgSatisfaction = ratedScores.Count > 0 ? ratedScores.Average() : (double?)null;
            return new DashboardKpisDto(tickets.Count, openTickets, resolvedTickets, overdueTickets, resolutionRate, avgSatisfaction);
        }, new DashboardKpisDto(tickets.Count, 0, 0, 0, 0, null));

        var fallbackSpan = TimeSpan.FromDays(_options.OnTimeResolutionTargetDays);

        // --- Bar: tickets by region ---
        var byRegion = Section("ticketsByRegion", () => tickets
            .GroupBy(t => t.Client?.Region ?? "Unspecified")
            .Select(g => new RegionTicketCountDto(g.Key, g.Count()))
            .OrderByDescending(r => r.TicketCount)
            .ToList(), new List<RegionTicketCountDto>());

        // --- Bar: tickets by failure type ---
        var byFailureType = Section("ticketsByFailureType", () => tickets
            .GroupBy(t => t.FailureType?.Name ?? "Unspecified")
            .Select(g => new FailureTypeTicketCountDto(g.Key, g.Count()))
            .OrderByDescending(f => f.TicketCount)
            .ToList(), new List<FailureTypeTicketCountDto>());

        // --- Bar: employee performance (resolved-ticket counts) ---
        var byEmployee = Section("ticketsByEmployee", () => tickets
            .Where(t => t.AssignedEmployee != null && t.ResolvedAt != null)
            .GroupBy(t => t.AssignedEmployee?.FullName ?? "Unknown employee")
            .Select(g => new EmployeeTicketCountDto(g.Key, g.Count()))
            .OrderByDescending(e => e.ResolvedCount)
            .ToList(), new List<EmployeeTicketCountDto>());

        // --- Donut: ticket status (every status represented, even at 0 — see GetOperationsOverviewAsync above for the same rationale) ---
        var byStatus = Enum.GetValues<TicketStatus>()
            .Select(status => new TicketStatusSliceDto(status.ToString(), tickets.Count(t => t.Status == status)))
            .ToList();

        // --- Donut: customer rating distribution (1-5 in half-star increments, every value represented) ---
        var possibleRatings = new[] { 1m, 1.5m, 2m, 2.5m, 3m, 3.5m, 4m, 4.5m, 5m };
        var ratingDistribution = possibleRatings
            .Select(stars => new RatingSliceDto(stars, tickets.Count(t => t.SatisfactionStars == stars)))
            .ToList();

        // --- Line: monthly tickets / resolved / on-time rate, last 6 calendar months (in Ethiopian local time, for consistency with every other date-bucketing decision in this app) ---
        var monthlyTrend = Section("monthlyTrend", () => BuildMonthlyTrend(tickets, fallbackSpan), new List<MonthlyPointDto>());

        SupportOverviewDto supportOverview;
        try
        {
            supportOverview = await GetSupportOverviewAsync(ct);
        }
        catch (Exception)
        {
            failedSections.Add("supportOverview");
            supportOverview = new SupportOverviewDto(0, 0, 0, [], [], []);
        }

        var result = new DashboardDataDto(kpis, byRegion, byFailureType, byEmployee, byStatus, ratingDistribution, monthlyTrend, supportOverview, failedSections);

        // Only cache a fully successful build — otherwise a transient failure
        // would be served from cache for the next two minutes.
        if (failedSections.Count == 0)
            _cache.Set(cacheKey, result, TimeSpan.FromMinutes(2));
        return result;
    }

    private List<MonthlyPointDto> BuildMonthlyTrend(List<Ticket> tickets, TimeSpan fallbackSpan)
    {
        var nowLocal = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(3));
        var months = Enumerable.Range(0, 6)
            .Select(i => new DateTime(nowLocal.Year, nowLocal.Month, 1).AddMonths(-5 + i))
            .ToList();

        return months.Select(monthStart =>
        {
            var monthEnd = monthStart.AddMonths(1);
            var monthLabel = monthStart.ToString("yyyy-MM");

            var inMonth = tickets.Where(t =>
            {
                var submittedLocal = t.DateSubmitted.ToOffset(TimeSpan.FromHours(3)).DateTime;
                return submittedLocal >= monthStart && submittedLocal < monthEnd;
            }).ToList();

            var resolvedInMonth = inMonth.Where(t => t.ResolvedAt != null).ToList();
            var withBoth = resolvedInMonth.Where(t => t.AssignedAt != null).ToList();
            var onTimeCount = withBoth.Count(t =>
                _officeTime.WorkingMinutesElapsed(t.AssignedAt!.Value, t.ResolvedAt!.Value) <=
                (t.ExpectedResolutionMinutes is int mins ? mins : fallbackSpan.TotalMinutes));
            var onTimeRate = withBoth.Count > 0 ? Math.Round(onTimeCount * 100.0 / withBoth.Count, 1) : (double?)null;

            return new MonthlyPointDto(monthLabel, inMonth.Count, resolvedInMonth.Count, onTimeRate);
        }).ToList();
    }

    /// <summary>
    /// Loads the client with every navigation the report needs in one
    /// pass (Include chain below) rather than N+1 queries per
    /// system/product — a client with several systems/products, each
    /// with several agreements and training records, would otherwise
    /// trigger a query per collection per item.
    /// </summary>
    public async Task<OverallClientReportDto?> GetOverallClientReportAsync(Guid clientId, CancellationToken ct = default)
    {
        var client = await _db.Clients.AsNoTracking()
            .Include(c => c.SystemProducts.Where(sp => !sp.IsDeleted))
                .ThenInclude(sp => sp.Agreements)
                    .ThenInclude(a => a.AgreementType)
            .Include(c => c.SystemProducts.Where(sp => !sp.IsDeleted))
                .ThenInclude(sp => sp.TrainingRecords)
                    .ThenInclude(tr => tr.TrainerEmployee)
            .FirstOrDefaultAsync(c => c.Id == clientId, ct);

        if (client is null)
            return null;

        var tickets = await _db.Tickets.AsNoTracking()
            .Where(t => t.ClientId == clientId)
            .Include(t => t.FailureType)
            .Include(t => t.AssignedEmployee)
            .OrderByDescending(t => t.DateSubmitted)
            .ToListAsync(ct);

        var surveys = await _db.SatisfactionSurveys.AsNoTracking()
            .Where(s => s.ClientId == clientId)
            .Include(s => s.Answers)
            .OrderByDescending(s => s.SubmittedAt)
            .ToListAsync(ct);

        var systemProducts = client.SystemProducts
            .OrderBy(sp => sp.Name)
            .Select(sp => new ClientReportSystemProductDto(
                sp.Id,
                sp.ReferenceNumber,
                sp.Name,
                sp.Description,
                sp.DeploymentDate,
                sp.TrainingCompletionStatus,
                sp.Agreements
                    .OrderByDescending(a => a.SignDate)
                    .Select(a => new ClientReportAgreementDto(
                        a.Id, a.AgreementType.Name, a.DocumentNumber,
                        a.SignDate, a.ExpiryDate, a.SupportWindowMonths, a.Status, a.BillingTier))
                    .ToList(),
                sp.TrainingRecords
                    .OrderByDescending(tr => tr.TrainingDate)
                    .Select(tr => new ClientReportTrainingRecordDto(
                        tr.Id, tr.TrainerEmployee.FullName, tr.TrainingDate, tr.Description, tr.FileName))
                    .ToList()))
            .ToList();

        var ticketDtos = tickets
            .Select(t => new ClientReportTicketDto(
                t.Id, t.Description, t.Category, t.FailureType?.Name, t.DateSubmitted,
                t.AssignedEmployee?.FullName, t.Status, t.SupportPhase, t.Chargeable, t.ChargeAmount,
                t.ResolvedAt, t.SatisfactionStars, t.SatisfactionScore, t.ClosureReason,
                t.AttachmentFileName, t.VoiceNoteFileName))
            .ToList();

        var surveyDtos = surveys
            .Select(s => new ClientReportSurveyDto(
                s.Id, s.TicketId, s.SubmittedAt,
                s.Answers.OrderBy(a => a.DisplayOrder)
                    .Select(a => new ClientReportSurveyAnswerDto(a.QuestionText, a.Rating))
                    .ToList(),
                s.SatisfactionComment))
            .ToList();

        var ratedTickets = tickets.Where(t => t.SatisfactionScore != null).ToList();
        var summary = new ClientReportSummaryDto(
            systemProducts.Count,
            systemProducts.Sum(sp => sp.Agreements.Count(a => a.Status == AgreementStatus.Active)),
            tickets.Count,
            tickets.Count(t => t.Status is not (TicketStatus.Resolved or TicketStatus.Closed or TicketStatus.AwaitingClientConfirmation)),
            tickets.Count(t => t.Status is TicketStatus.Resolved or TicketStatus.Closed or TicketStatus.AwaitingClientConfirmation),
            ratedTickets.Count > 0 ? Math.Round(ratedTickets.Average(t => t.SatisfactionScore!.Value), 1) : (double?)null,
            surveyDtos.Count);

        return new OverallClientReportDto(
            client.Id, client.Name, client.AccountRefId, client.PhoneNumber, client.Email,
            client.Office, client.Location, client.Region, client.Zone, client.City, client.Woreda,
            client.AccountStatus, client.OnboardingDate,
            systemProducts, ticketDtos, surveyDtos, summary);
    }
}
