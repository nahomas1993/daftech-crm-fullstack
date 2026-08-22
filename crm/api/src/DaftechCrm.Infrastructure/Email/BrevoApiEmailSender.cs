using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

namespace DaftechCrm.Infrastructure.Email;

/// <summary>
/// Sends transactional email via Brevo's HTTP Transactional Email API
/// (POST /v3/smtp/email) instead of an SMTP socket connection. Exists
/// because some hosts (e.g. Render's free tier) block outbound SMTP
/// ports 25/465/587 but allow ordinary HTTPS — see EmailProvider. Used
/// in place of MailKitEmailSender when Email:Provider = BrevoApi.
///
/// Same retry/degrade-gracefully shape as MailKitEmailSender: transient
/// failures (5xx, timeouts, connection errors) retry with exponential
/// backoff; permanent failures (401 bad key, 400 bad request) fail fast
/// since retrying won't change the outcome.
/// </summary>
public class BrevoApiEmailSender : IEmailSender
{
    private readonly HttpClient _http;
    private readonly BrevoApiOptions _options;
    private readonly ILogger<BrevoApiEmailSender> _logger;
    private readonly ResiliencePipeline _retryPipeline;

    public BrevoApiEmailSender(HttpClient http, IOptions<BrevoApiOptions> options, ILogger<BrevoApiEmailSender> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
        _retryPipeline = BuildRetryPipeline(_options, logger);

        // Uri's relative-combination rules drop the last base segment when
        // the base address has no trailing slash (e.g. ".../v3" + "smtp/email"
        // => ".../smtp/email", losing "/v3" -> Brevo returns 404). Force a
        // trailing slash so "v3" is treated as a directory, not a filename.
        var baseUrl = _options.ApiBaseUrl.EndsWith('/') ? _options.ApiBaseUrl : _options.ApiBaseUrl + "/";
        _http.BaseAddress = new Uri(baseUrl);
        _http.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
    }

    public async Task<EmailSendResult> SendAsync(string toAddress, string toName, string subject, string htmlBody, CancellationToken ct = default)
    {
        try
        {
            await _retryPipeline.ExecuteAsync(async token => await SendOnceAsync(toAddress, toName, subject, htmlBody, token), ct);
            return new EmailSendResult(true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {ToAddress} after all retry attempts.", toAddress);
            return new EmailSendResult(false, ex.Message);
        }
    }

    private async Task SendOnceAsync(string toAddress, string toName, string subject, string htmlBody, CancellationToken ct)
    {
        var payload = new BrevoSendRequest(
            Sender: new BrevoContact(_options.FromName, _options.FromAddress),
            To: [new BrevoContact(toName, toAddress)],
            Subject: subject,
            HtmlContent: htmlBody
        );

        using var request = new HttpRequestMessage(HttpMethod.Post, "smtp/email")
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Add("api-key", _options.ApiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _http.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            var message = $"Brevo API returned {(int)response.StatusCode} {response.StatusCode}: {body}";

            // 5xx and 429 are worth retrying (transient); 4xx (bad key, bad
            // request, unverified sender) will fail identically every time.
            if ((int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.TooManyRequests)
                throw new BrevoTransientException(message);

            throw new BrevoPermanentException(message);
        }
    }

    private static ResiliencePipeline BuildRetryPipeline(BrevoApiOptions options, ILogger logger)
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = Math.Max(0, options.MaxRetryAttempts - 1),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromSeconds(options.BaseDelaySeconds),
                ShouldHandle = new PredicateBuilder()
                    .Handle<BrevoTransientException>()
                    .Handle<HttpRequestException>()
                    .Handle<TaskCanceledException>()
                    .Handle<TimeoutException>(),
                OnRetry = args =>
                {
                    logger.LogWarning(
                        "Email send attempt {AttemptNumber} failed, retrying in {Delay}. Reason: {Reason}",
                        args.AttemptNumber + 1, args.RetryDelay, args.Outcome.Exception?.Message);
                    return ValueTask.CompletedTask;
                },
            })
            .Build();
    }

    private record BrevoSendRequest(
        [property: JsonPropertyName("sender")] BrevoContact Sender,
        [property: JsonPropertyName("to")] BrevoContact[] To,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("htmlContent")] string HtmlContent
    );

    private record BrevoContact(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("email")] string Email
    );

    /// <summary>Retryable: rate limits, server errors.</summary>
    private class BrevoTransientException(string message) : Exception(message);

    /// <summary>Not retryable: bad API key, unverified sender, malformed request — same result every attempt.</summary>
    private class BrevoPermanentException(string message) : Exception(message);
}
