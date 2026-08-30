namespace DaftechCrm.Application.Interfaces;

/// <summary>Result of a successful upload — enough for the caller to persist a reference and later fetch or delete the file.</summary>
public record StoredFileResult(string StorageKey, string FileUrl, string OriginalFileName, long SizeBytes, string ContentType);

/// <summary>Raw bytes plus metadata for a downloaded file, so the controller can stream it back with the right headers.</summary>
public record RetrievedFile(Stream Content, string ContentType, string OriginalFileName);

/// <summary>
/// Outcome of trying to fetch an attachment/voice-note that a record
/// (Ticket, etc.) references. Distinguishes "there was never anything
/// here" from "there was supposed to be a file here and it's gone" —
/// collapsing both into a single null (as a plain RetrievedFile? does)
/// made every 404 look identical to the caller and to the person seeing
/// it, whether the ticket simply had no attachment or its stored file had
/// been lost. Reported() and NotFound() are the constructors most callers
/// need; NoFile() covers the "nothing was ever attached" case.
/// </summary>
public record FileRetrievalResult(RetrievedFile? File, FileRetrievalStatus Status)
{
    public static FileRetrievalResult Found(RetrievedFile file) => new(file, FileRetrievalStatus.Found);
    public static FileRetrievalResult NoFile() => new(null, FileRetrievalStatus.NoFileAttached);
    public static FileRetrievalResult Lost() => new(null, FileRetrievalStatus.FileLost);
}

public enum FileRetrievalStatus
{
    /// <summary>The file was retrieved successfully.</summary>
    Found,

    /// <summary>Nothing was ever attached — there's no StorageKey to look up.</summary>
    NoFileAttached,

    /// <summary>A StorageKey is on record, but the storage backend no longer has it.</summary>
    FileLost,
}

/// <summary>
/// Outcome of an attachment/scanned-file/training-file permission check.
/// Distinguishes "the owning record doesn't exist" (→ 404) from "it
/// exists, but this caller isn't allowed to see it" (→ 403 via
/// ForbidOwnership) — collapsing both into a single bool, as the older
/// CanAccessAttachmentAsync signature did, made an authenticated caller
/// who'd simply lost access (e.g. a ticket reassigned away from them)
/// indistinguishable from a nonexistent resource, on both the wire and in
/// the UI that has to explain the failure to a person.
/// </summary>
public enum AttachmentAccessResult
{
    /// <summary>The caller may access this record's file.</summary>
    Granted,

    /// <summary>The owning record (ticket, agreement, training record, etc.) does not exist.</summary>
    RecordNotFound,

    /// <summary>The record exists, but this caller is not permitted to access its file.</summary>
    Forbidden,
}

/// <summary>
/// Thrown when an upload fails validation (bad extension, oversized file)
/// — distinct from unexpected I/O failures so callers can return 400 vs 500.
/// </summary>
public class FileValidationException : Exception
{
    public FileValidationException(string message) : base(message) { }
}

/// <summary>
/// Stores and retrieves uploaded files (agreement scans today; generic
/// enough for any future upload use). LocalFileStorageService is the only
/// implementation, but callers depend only on this interface so swapping
/// in S3/Azure Blob later doesn't touch calling code.
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Validates and saves a file, organized under a year/month folder
    /// with a GUID-based filename. Throws FileValidationException if the
    /// extension isn't allowed or the file exceeds the configured max size.
    /// </summary>
    Task<StoredFileResult> SaveAsync(Stream content, string originalFileName, string contentType, CancellationToken ct = default);

    /// <summary>Retrieves a previously saved file by its storage key (as returned in StoredFileResult.StorageKey).</summary>
    Task<RetrievedFile?> GetAsync(string storageKey, CancellationToken ct = default);

    /// <summary>Deletes a previously saved file. A no-op (not an error) if the file doesn't exist.</summary>
    Task DeleteAsync(string storageKey, CancellationToken ct = default);

    /// <summary>Round-trips a tiny probe file — used by StorageHealthCheck to verify the storage backend is writable and readable.</summary>
    Task<bool> ProbeAsync(CancellationToken ct = default);
}
