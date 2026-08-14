using DaftechCrm.Application.Interfaces;
using DaftechCrm.Application.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using Polly;
using Polly.Retry;

namespace DaftechCrm.Infrastructure.Email;

/// <summary>
/// SRS v2.0 §4.3.1 / §3.2 Account Provisioning &amp; Credential Service: sends
/// account-credential and notification emails over SMTP via MailKit.
///
/// Transient failures (connection drops, timeouts, temporary SMTP 4xx
/// responses) are retried automatically with exponential backoff + jitter
/// via Polly — see SmtpOptions.MaxRetryAttempts/BaseDelaySeconds. Permanent
/// failures (auth rejected, invalid recipient) are not worth retrying and
/// fail fast. Either way, if every attempt fails the caller
/// (AccountCredentialService) records the failure so the Admin can retry
/// manually or fall back to the on-screen reveal — per NFR-9, credentials
/// are still only ever shown in plaintext once.
/// </summary>
public class MailKitEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;
    private readonly ILogger<MailKitEmailSender> _logger;
    private readonly ResiliencePipeline _retryPipeline;

    public MailKitEmailSender(IOptions<SmtpOptions> options, ILogger<MailKitEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
        _retryPipeline = BuildRetryPipeline(_options, logger);
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
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_options.FromName, _options.FromAddress));
        message.To.Add(new MailboxAddress(toName, toAddress));
        message.Subject = subject;
        message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

        using var client = new SmtpClient();
        var socketOptions = _options.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto;
        await client.ConnectAsync(_options.Host, _options.Port, socketOptions, ct);

        if (!string.IsNullOrEmpty(_options.Username))
            await client.AuthenticateAsync(_options.Username, _options.Password, ct);

        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);
    }

    private static ResiliencePipeline BuildRetryPipeline(SmtpOptions options, ILogger logger)
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                // MaxRetryAttempts counts the *first* attempt too, so Polly's
                // "retries beyond the first try" count is one less.
                MaxRetryAttempts = Math.Max(0, options.MaxRetryAttempts - 1),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromSeconds(options.BaseDelaySeconds),
                // Auth failures, protocol errors, and connection drops are worth
                // retrying (transient). A command rejected with a permanent 5xx
                // status (bad recipient, message rejected) will fail the same way
                // every time, so retrying wastes time without changing the outcome.
                ShouldHandle = new PredicateBuilder()
                    .Handle<SmtpCommandException>(IsTransientSmtpError)
                    .Handle<SmtpProtocolException>()
                    .Handle<System.Net.Sockets.SocketException>()
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

    /// <summary>SMTP 4xx codes are transient (server busy, greylisting, etc.) — worth retrying. 5xx codes are permanent rejections.</summary>
    private static bool IsTransientSmtpError(SmtpCommandException ex) =>
        (int)ex.StatusCode is >= 400 and < 500;
}
