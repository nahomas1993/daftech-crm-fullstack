using DaftechCrm.Application.Interfaces;
using DaftechCrm.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace DaftechCrm.Infrastructure.Storage;

/// <summary>
/// Decorates the configured (primary) IFileStorageService with a
/// recovery path for files saved under a PREVIOUSLY-active provider.
///
/// The Storage:Provider setting picks exactly one implementation to
/// register (see DependencyInjection.AddInfrastructure) — but that
/// setting can change over the app's lifetime (e.g. moving off
/// LocalFileSystem, or off Cloudinary, onto Postgres for durability on
/// hosts without persistent disk). Every ticket/agreement/training row
/// created before that switch still has a StorageKey pointing at the OLD
/// provider, and once the DI registration flips, plain GetAsync on the
/// new provider 404s every one of those older files forever — this was
/// the root cause behind "This file could not be found" on files that
/// clearly still existed somewhere.
///
/// SaveAsync and DeleteAsync always act ONLY on the primary provider —
/// every new upload, and every explicit delete of a still-current
/// attachment, goes through the currently-configured backend, exactly as
/// before. GetAsync is the one call this class widens: if the primary
/// provider doesn't have the key, it walks the other configured
/// providers (fixed order, see DependencyInjection) and returns the
/// first hit.
///
/// This is read-only recovery, not a migration — a file found via
/// fallback is not copied into the primary provider or re-pointed on the
/// owning row, so the same fallback lookup runs again next time it's
/// requested. That's an acceptable trade for now: it turns a permanent
/// 404 for pre-cutover files into a (slightly slower) successful
/// download, without silently rewriting storage keys behind the rest of
/// the app's back.
/// </summary>
public class FallbackFileStorageService : IFileStorageService
{
    private readonly IFileStorageService _primary;
    private readonly IReadOnlyList<(StorageProvider Provider, IFileStorageService Service)> _fallbacks;
    private readonly ILogger<FallbackFileStorageService> _logger;

    public FallbackFileStorageService(
        IFileStorageService primary,
        IReadOnlyList<(StorageProvider Provider, IFileStorageService Service)> fallbacks,
        ILogger<FallbackFileStorageService> logger)
    {
        _primary = primary;
        _fallbacks = fallbacks;
        _logger = logger;
    }

    public Task<StoredFileResult> SaveAsync(Stream content, string originalFileName, string contentType, CancellationToken ct = default) =>
        _primary.SaveAsync(content, originalFileName, contentType, ct);

    public async Task<RetrievedFile?> GetAsync(string storageKey, CancellationToken ct = default)
    {
        var fromPrimary = await TryGetAsync(_primary, "primary", storageKey, ct);
        if (fromPrimary is not null)
            return fromPrimary;

        foreach (var (provider, service) in _fallbacks)
        {
            var found = await TryGetAsync(service, provider.ToString(), storageKey, ct);
            if (found is not null)
            {
                _logger.LogInformation(
                    "Recovered stored file {StorageKey} from fallback provider {Provider} — the primary provider no longer has it.",
                    storageKey, provider);
                return found;
            }
        }

        return null;
    }

    /// <summary>
    /// Deletes only from the primary provider. A key that only exists
    /// under a fallback provider is left alone — this class is a
    /// read-recovery path, not a cross-provider delete, and reaching into
    /// an old provider to delete something the rest of the app no longer
    /// has a live record of pointing at is more likely to surprise
    /// someone than help them.
    /// </summary>
    public Task DeleteAsync(string storageKey, CancellationToken ct = default) =>
        _primary.DeleteAsync(storageKey, ct);

    public Task<bool> ProbeAsync(CancellationToken ct = default) =>
        _primary.ProbeAsync(ct);

    private async Task<RetrievedFile?> TryGetAsync(IFileStorageService storage, string label, string storageKey, CancellationToken ct)
    {
        try
        {
            return await storage.GetAsync(storageKey, ct);
        }
        catch (Exception ex)
        {
            // A fallback provider throwing (bad/missing credentials,
            // network error, a key that isn't even shaped like that
            // provider's format) must not block trying the next one, or
            // stop the primary's own result from being the final answer.
            _logger.LogDebug(ex, "Storage lookup for {StorageKey} failed against {Label} provider", storageKey, label);
            return null;
        }
    }
}
