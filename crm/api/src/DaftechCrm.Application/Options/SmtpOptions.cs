namespace DaftechCrm.Application.Options;

/// <summary>Bound from appsettings.json ("Smtp" section).</summary>
public class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = "smtp.example.com";
    public int Port { get; set; } = 587;
    public bool UseStartTls { get; set; } = true;
    public string Username { get; set; } = default!;
    public string Password { get; set; } = default!;
    public string FromAddress { get; set; } = "no-reply@daftech.et";
    public string FromName { get; set; } = "DAFTECH CRM";

    /// <summary>Number of send attempts before giving up, including the first attempt. E.g. 3 means 1 initial try + 2 retries.</summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>Base delay for exponential backoff between retries — actual delay is roughly BaseDelaySeconds * 2^attempt, plus jitter.</summary>
    public int BaseDelaySeconds { get; set; } = 2;
}
