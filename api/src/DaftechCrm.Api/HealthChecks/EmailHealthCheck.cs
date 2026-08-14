using DaftechCrm.Application.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace DaftechCrm.Api.HealthChecks;

/// <summary>
/// Confirms the SMTP server is reachable — connects and, if credentials
/// are configured, authenticates, but never actually sends a message (a
/// health check shouldn't spam real emails every time it's polled).
///
/// Deliberately NOT tagged "ready": email is a soft dependency — if SMTP
/// is briefly down, the API should keep serving tickets/clients/reports
/// traffic rather than being pulled out of rotation. It's checked under
/// the unqualified /health endpoint (which reports Degraded rather than
/// failing the deployment) so operators can still see it in dashboards.
/// </summary>
public class EmailHealthCheck : IHealthCheck
{
    private readonly SmtpOptions _options;

    public EmailHealthCheck(IOptions<SmtpOptions> options) => _options = options.Value;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            using var client = new SmtpClient();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));

            var socketOptions = _options.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;
            await client.ConnectAsync(_options.Host, _options.Port, socketOptions, timeoutCts.Token);

            if (!string.IsNullOrEmpty(_options.Username))
                await client.AuthenticateAsync(_options.Username, _options.Password, timeoutCts.Token);

            await client.DisconnectAsync(true, timeoutCts.Token);

            return HealthCheckResult.Healthy("SMTP server is reachable.");
        }
        catch (Exception ex)
        {
            // Degraded, not Unhealthy: email failing shouldn't fail the whole
            // health check response or trigger orchestrator restarts.
            return HealthCheckResult.Degraded("SMTP server is not reachable.", ex);
        }
    }
}
