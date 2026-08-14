namespace DaftechCrm.Application.Options;

/// <summary>Bound from appsettings.json ("BrevoApi" section). Only read when Email:Provider is BrevoApi.</summary>
public class BrevoApiOptions
{
    public const string SectionName = "BrevoApi";

    public string ApiKey { get; set; } = default!;
    public string ApiBaseUrl { get; set; } = "https://api.brevo.com/v3";
    public string FromAddress { get; set; } = "no-reply@daftech.et";
    public string FromName { get; set; } = "DAFTECH CRM";

    /// <summary>Number of send attempts before giving up, including the first attempt. E.g. 3 means 1 initial try + 2 retries.</summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>Base delay for exponential backoff between retries — actual delay is roughly BaseDelaySeconds * 2^attempt, plus jitter.</summary>
    public int BaseDelaySeconds { get; set; } = 2;

    /// <summary>Per-request timeout in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 15;
}
