using DaftechCrm.Application.Interfaces;
using DaftechCrm.Application.Options;
using DaftechCrm.Domain.Entities;
using DaftechCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DaftechCrm.Infrastructure.Storage;

/// <summary>
/// Stores uploaded files (ticket attachments, voice-note recordings) as
/// bytes in this app's own Postgres database — see the StoredFile entity —
/// instead of a third-party host like Cloudinary or the local container
/// filesystem (which is wiped on every redeploy on hosts without a
/// persistent disk, e.g. Render's free tier). StorageKey is the
/// StoredFile row's Id as a string, matching the same contract
/// LocalFileStorageService and CloudinaryFileStorageService already use,
/// so callers (TicketService, AgreementService) don't need to know or
/// care which provider is active.
///
/// Takes an IServiceScopeFactory rather than AppDbContext directly:
/// registered as a singleton alongside the other IFileStorageService
/// implementations (see DependencyInjection.AddInfrastructure), but
/// AppDbContext is scoped, so each call opens its own short-lived scope.
/// </summary>
public class PostgresFileStorageService : IFileStorageService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly StorageOptions _options;
    private readonly ILogger<PostgresFileStorageService> _logger;

    public PostgresFileStorageService(IServiceScopeFactory scopeFactory, IOptions<StorageOptions> options, ILogger<PostgresFileStorageService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
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

        content.Position = 0;
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();

        var row = new StoredFile
        {
            OriginalFileName = originalFileName,
            ContentType = contentType,
            SizeBytes = bytes.LongLength,
            Content = bytes,
        };

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.StoredFilesSet.Add(row);
        await db.SaveChangesAsync(ct);

        _logger.LogInformation("Stored uploaded file {StorageKey} ({SizeBytes} bytes) in Postgres", row.Id, bytes.LongLength);

        return new StoredFileResult(row.Id.ToString(), BuildFileUrl(row.Id), originalFileName, bytes.LongLength, contentType);
    }

    public async Task<RetrievedFile?> GetAsync(string storageKey, CancellationToken ct = default)
    {
        if (!Guid.TryParse(storageKey, out var id))
            return null;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.StoredFilesSet.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, ct);
        if (row is null)
            return null;

        return new RetrievedFile(new MemoryStream(row.Content), row.ContentType, row.OriginalFileName);
    }

    public async Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        if (!Guid.TryParse(storageKey, out var id))
            return;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.StoredFilesSet.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (row is null)
            return;

        db.StoredFilesSet.Remove(row);
        await db.SaveChangesAsync(ct);
        _logger.LogInformation("Deleted stored file {StorageKey} from Postgres", storageKey);
    }

    public async Task<bool> ProbeAsync(CancellationToken ct = default)
    {
        try
        {
            // Writes/reads a StoredFile row directly rather than going
            // through SaveAsync — the probe file's extension (.tmp) isn't
            // necessarily in the configured AllowedExtensions list, and
            // this probe should only be verifying the database round-trip
            // works, not re-validating upload rules.
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var probe = new StoredFile
            {
                OriginalFileName = "probe.tmp",
                ContentType = "text/plain",
                Content = System.Text.Encoding.UTF8.GetBytes("daftech-crm storage probe"),
            };
            probe.SizeBytes = probe.Content.LongLength;

            db.StoredFilesSet.Add(probe);
            await db.SaveChangesAsync(ct);

            var readBack = await db.StoredFilesSet.AsNoTracking().FirstOrDefaultAsync(f => f.Id == probe.Id, ct);

            db.StoredFilesSet.Remove(probe);
            await db.SaveChangesAsync(ct);

            return readBack is not null && System.Text.Encoding.UTF8.GetString(readBack.Content) == "daftech-crm storage probe";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Postgres storage health probe failed.");
            return false;
        }
    }

    // Informational only, same as LocalFileStorageService — the real
    // download route is GET /api/tickets/{id}/attachment (or similar),
    // authorized per-ticket rather than exposing raw storage keys.
    private static string BuildFileUrl(Guid id) => $"/storage/db/{id}";
}
