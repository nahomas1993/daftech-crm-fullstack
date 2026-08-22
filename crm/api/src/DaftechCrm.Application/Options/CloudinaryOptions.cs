namespace DaftechCrm.Application.Options;

/// <summary>
/// Bound from appsettings.json ("Cloudinary" section). Only read when
/// Storage:Provider is Cloudinary. Free-tier Cloudinary account is
/// sufficient for ticket attachments (25 GB storage/bandwidth).
/// Credentials come from the account dashboard at cloudinary.com/console —
/// CloudName, ApiKey, and ApiSecret should be set via environment
/// variables in production, never committed to source.
/// </summary>
public class CloudinaryOptions
{
    public const string SectionName = "Cloudinary";

    public string CloudName { get; set; } = default!;
    public string ApiKey { get; set; } = default!;
    public string ApiSecret { get; set; } = default!;

    /// <summary>Folder prefix under the Cloudinary account, e.g. "daftech-crm/ticket-attachments".</summary>
    public string Folder { get; set; } = "daftech-crm/ticket-attachments";

    /// <summary>Per-request timeout in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 20;
}
