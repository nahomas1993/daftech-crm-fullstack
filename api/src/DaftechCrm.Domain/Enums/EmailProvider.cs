namespace DaftechCrm.Domain.Enums;

/// <summary>
/// Which mechanism IEmailSender uses to actually send mail. Smtp works
/// anywhere a host allows outbound SMTP (self-hosted/VPS/Docker); BrevoApi
/// sends over HTTPS instead, which matters on hosts that block outbound
/// SMTP ports on free/starter tiers (e.g. Render — see
/// https://render.com/docs, "free web services" SMTP port restriction).
/// Swapping providers is a config change (EmailProvider), never a code
/// change — callers only ever see IEmailSender.
/// </summary>
public enum EmailProvider
{
    Smtp,
    BrevoApi
}
