using DaftechCrm.Domain.Enums;

namespace DaftechCrm.Application.Options;

/// <summary>
/// Bound from appsettings.json ("Email" section). Selects which
/// IEmailSender implementation DI wires up — see EmailProvider for the
/// deployment-portability rationale.
/// </summary>
public class EmailOptions
{
    public const string SectionName = "Email";

    public EmailProvider Provider { get; set; } = EmailProvider.Smtp;
}
