using DaftechCrm.Application.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace DaftechCrm.Api.HealthChecks;

/// <summary>
/// Confirms Brevo's API is reachable and the configured API key is
/// valid, via a lightweight authenticated GET (no email is sent). Used
/// in place of EmailHealthCheck when Email:Provider = BrevoApi.
///
/// Same soft-dependency contract as EmailHealthCheck: tagged "email",
/// not "ready" — a Brevo outage shouldn't pull the API out of rotation.
/// </summary>
public class BrevoApiHealthCheck : IHealthCheck
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly BrevoApiOptions _options;

    public BrevoApiHealthCheck(IHttpClientFactory httpClientFactory, IOptions<BrevoApiOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(5));

            using var request = new HttpRequestMessage(HttpMethod.Get, $"{_options.ApiBaseUrl.TrimEnd('/')}/account");
            request.Headers.Add("api-key", _options.ApiKey);

            using var response = await client.SendAsync(request, timeoutCts.Token);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy("Brevo API is reachable.")
                : HealthCheckResult.Degraded($"Brevo API returned {(int)response.StatusCode} {response.StatusCode}.");
        }
        catch (Exception ex)
        {
            // Degraded, not Unhealthy: email failing shouldn't fail the whole
            // health check response or trigger orchestrator restarts.
            return HealthCheckResult.Degraded("Brevo API is not reachable.", ex);
        }
    }
}
