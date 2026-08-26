using DaftechCrm.Application.Interfaces;
using DaftechCrm.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DaftechCrm.Infrastructure.Storage;

/// <summary>
/// Stores uploaded files on the local file system under
/// {RootPath}/{yyyy}/{MM}/{guid}{ext}. StorageKey (what callers persist,
/// e.g. on Agreement.ScannedFileUrl) is that relative path — never an
/// absolute path — so the root can move between environments (dev machine
/// vs. the Docker volume mount) without invalidating stored references.
/// </summary>
public class LocalFileStorageService : IFileStorageService
{
    private readonly StorageOptions _options;
    private readonly ILogger<LocalFileStorageService> _logger;
    private readonly string _rootPath;

    public LocalFileStorageService(IOptions<StorageOptions> options, ILogger<LocalFileStorageService> logger)
    {
        _options = options.Value;
        _logger = logger;
        // Resolve to an absolute path once so every later operation is unambiguous
        // regardless of the process's current working directory.
        _rootPath = Path.GetFullPath(_options.RootPath);
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<StoredFileResult> SaveAsync(Stream content, string originalFileName, string contentType, CancellationToken ct = default)
    {
        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();

        if (string.IsNullOrEmpty(extension) || !_options.AllowedExtensions.Contains(extension))
        {
            throw new FileValidationException(
                $"File type '{extension}' is not allowed. Allowed types: {string.Join(", ", _options.AllowedExtensions)}");
        }

        if (content.Length > _options.MaxFileSizeBytes)
        {
            var maxMb = _options.MaxFileSizeBytes / (1024.0 * 1024.0);
            throw new FileValidationException($"File exceeds the maximum allowed size of {maxMb:0.#} MB.");
        }

        var now = DateTimeOffset.UtcNow;
        var relativeFolder = Path.Combine(now.Year.ToString(), now.Month.ToString("00"));
        var absoluteFolder = Path.Combine(_rootPath, relativeFolder);
        Directory.CreateDirectory(absoluteFolder);

        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var storageKey = Path.Combine(relativeFolder, storedFileName).Replace('\\', '/');
        var absolutePath = Path.Combine(absoluteFolder, storedFileName);

        long bytesWritten;
        try
        {
            await using (var fileStream = new FileStream(absolutePath, FileMode.CreateNew, FileAccess.Write))
            {
                content.Position = 0;
                await content.CopyToAsync(fileStream, ct);
                bytesWritten = fileStream.Length;
            }
        }
        catch
        {
            // Don't leave a partially-written file behind on a failed/aborted copy.
            if (File.Exists(absolutePath))
                File.Delete(absolutePath);
            throw;
        }

        // A truncated write (client disconnected mid-upload, disk full,
        // etc.) leaves a 0-byte file on disk that would otherwise be
        // reported back as a "successful" save — clean it up and fail
        // loudly instead, same as the Postgres provider.
        if (bytesWritten == 0)
        {
            File.Delete(absolutePath);
            throw new FileValidationException(
                "The uploaded file was empty or could not be read. Please try uploading it again.");
        }

        var effectiveContentType = string.IsNullOrWhiteSpace(contentType)
            ? "application/octet-stream"
            : contentType;

        _logger.LogInformation("Stored uploaded file {StorageKey} ({SizeBytes} bytes)", storageKey, bytesWritten);

        return new StoredFileResult(storageKey, BuildFileUrl(storageKey), originalFileName, bytesWritten, effectiveContentType);
    }

    public Task<RetrievedFile?> GetAsync(string storageKey, CancellationToken ct = default)
    {
        var absolutePath = ResolveAndValidatePath(storageKey);

        if (!File.Exists(absolutePath))
            return Task.FromResult<RetrievedFile?>(null);

        // A 0-byte file on disk (e.g. from a truncated write before the
        // guard in SaveAsync existed) is corrupt, not a real download —
        // treat it the same as "file lost" rather than streaming back
        // nothing with a 200.
        if (new FileInfo(absolutePath).Length == 0)
        {
            _logger.LogWarning("Stored file {StorageKey} exists but is empty on disk — treating as lost.", storageKey);
            return Task.FromResult<RetrievedFile?>(null);
        }

        var stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read);
        var contentType = ContentTypeFor(Path.GetExtension(absolutePath));
        var originalFileName = Path.GetFileName(absolutePath);

        return Task.FromResult<RetrievedFile?>(new RetrievedFile(stream, contentType, originalFileName));
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        var absolutePath = ResolveAndValidatePath(storageKey);

        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
            _logger.LogInformation("Deleted stored file {StorageKey}", storageKey);
        }

        return Task.CompletedTask;
    }

    public async Task<bool> ProbeAsync(CancellationToken ct = default)
    {
        try
        {
            var probeFolder = Path.Combine(_rootPath, ".health");
            Directory.CreateDirectory(probeFolder);
            var probePath = Path.Combine(probeFolder, $"probe-{Guid.NewGuid():N}.tmp");

            await File.WriteAllTextAsync(probePath, "ok", ct);
            var readBack = await File.ReadAllTextAsync(probePath, ct);
            File.Delete(probePath);

            return readBack == "ok";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Storage health probe failed.");
            return false;
        }
    }

    /// <summary>
    /// Resolves a storage key to an absolute path and verifies it stays
    /// inside the storage root — blocks path traversal (e.g. a storage
    /// key containing "../../") from reading or deleting arbitrary files
    /// on the host.
    /// </summary>
    private string ResolveAndValidatePath(string storageKey)
    {
        var combined = Path.GetFullPath(Path.Combine(_rootPath, storageKey));

        if (!combined.StartsWith(_rootPath, StringComparison.Ordinal))
            throw new FileValidationException("Invalid storage key.");

        return combined;
    }

    // Note: FileUrl here is informational only (not used by the real download route,
    // which is GET /api/agreements/{id}/scanned-file — authorized per-agreement rather
    // than exposing raw storage keys). Kept for callers that just want a debug hint.
    private static string BuildFileUrl(string storageKey) => $"/storage/{storageKey}";

    private static string ContentTypeFor(string extension) => extension.ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".doc" => "application/msword",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        _ => "application/octet-stream",
    };
}
