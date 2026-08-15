namespace DaftechCrm.Domain.Entities;

/// <summary>
/// Binary content for an uploaded file (ticket attachment or voice note),
/// stored directly in Postgres instead of a third-party host like
/// Cloudinary. The row's Id (as a string) is exactly what
/// PostgresFileStorageService hands back as StorageKey — the same
/// contract LocalFileStorageService and CloudinaryFileStorageService
/// already use — so Ticket.AttachmentStorageKey / VoiceNoteStorageKey
/// don't change shape at all when the storage provider changes.
/// Content lives in this dedicated table (not inline on Ticket) so
/// listing/searching tickets never has to load file bytes.
/// </summary>
public class StoredFile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OriginalFileName { get; set; } = default!;
    public string ContentType { get; set; } = default!;
    public long SizeBytes { get; set; }
    public byte[] Content { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
