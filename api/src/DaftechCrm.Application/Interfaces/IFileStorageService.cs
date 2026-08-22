namespace DaftechCrm.Application.Interfaces;

/// <summary>Result of a successful upload — enough for the caller to persist a reference and later fetch or delete the file.</summary>
public record StoredFileResult(string StorageKey, string FileUrl, string OriginalFileName, long SizeBytes, string ContentType);

/// <summary>Raw bytes plus metadata for a downloaded file, so the controller can stream it back with the right headers.</summary>
public record RetrievedFile(Stream Content, string ContentType, string OriginalFileName);

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
