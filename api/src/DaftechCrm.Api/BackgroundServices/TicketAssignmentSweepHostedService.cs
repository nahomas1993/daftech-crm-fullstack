using DaftechCrm.Application.Interfaces;

namespace DaftechCrm.Api.BackgroundServices;

/// <summary>
/// Sweeps for tickets left queued (Status=Submitted, no assignee) because
/// they were submitted during lunch, after office close, or on Sunday —
/// see TicketService.SubmitFromClientAsync — and assigns any that have
/// become assignable now that office hours have resumed (see
/// IEthiopianTimeService, TicketService.AssignQueuedTicketsAsync).
///
/// Runs every 5 minutes — frequent enough that a ticket waiting for lunch
/// to end or for the next working day never sits idle for long once its
/// window opens, without hammering the database. (Tighter than
/// AutoCloseTicketsHostedService's 15-minute interval since a queued
/// ticket's whole point is to start being worked on as soon as the office
/// opens, not just "eventually".)
/// </summary>
public class TicketAssignmentSweepHostedService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    private readonly IServiceProvider _services;
    private readonly ILogger<TicketAssignmentSweepHostedService> _logger;

    public TicketAssignmentSweepHostedService(IServiceProvider services, ILogger<TicketAssignmentSweepHostedService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var ticketService = scope.ServiceProvider.GetRequiredService<ITicketService>();
                var assignedCount = await ticketService.AssignQueuedTicketsAsync(stoppingToken);
                if (assignedCount > 0)
                    _logger.LogInformation("Assigned {Count} queued ticket(s) now that office hours have resumed.", assignedCount);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error while sweeping queued tickets for assignment.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown.
            }
        }
    }
}
