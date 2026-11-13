using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Domain.Entities;
using DaftechCrm.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DaftechCrm.Application.Services;

public class AgreementService : IAgreementService
{
    private readonly IAppDbContext _db;
    private readonly IFileStorageService _storage;
    private readonly ReferenceNumberService _referenceNumbers;
    private readonly ISystemProductService _systemProducts;
    private readonly ILogger<AgreementService> _logger;

    public AgreementService(
        IAppDbContext db, IFileStorageService storage, ReferenceNumberService referenceNumbers,
        ISystemProductService systemProducts, ILogger<AgreementService> logger)
    {
        _db = db;
        _storage = storage;
        _referenceNumbers = referenceNumbers;
        _systemProducts = systemProducts;
        _logger = logger;
    }

    /// <summary>
    /// Creates (signs) a new agreement — always an insert, never overwrites
    /// or updates an existing agreement, even a prior one for the same
    /// SystemProduct/AgreementType. If the resolved AgreementType is
    /// Support, requires the same SystemProduct's TrainingCompletionStatus
    /// to already be Completed (see ISystemProductService.HasCompletedTrainingAsync/
    /// MarkTrainingCompletedAsync) — training must finish before support
    /// can be signed, per system/product, not client-wide.
    /// </summary>
    public async Task<AgreementDto> CreateAsync(CreateAgreementRequest request, CancellationToken ct = default)
    {
        RequiredFieldValidator.EnsureAllPresent(
            ("Agreement Place", request.AgreementPlace)
        );
        if (request.SupportWindowMonths <= 0)
            throw new ValidationException("Support window (months) must be greater than zero.");

        var systemProduct = await _db.SystemProducts.Include(s => s.Client)
            .FirstOrDefaultAsync(s => s.Id == request.SystemProductId && !s.IsDeleted, ct)
            ?? throw new InvalidOperationException("System/Product not found.");

        var agreementType = await _db.AgreementTypes.FirstOrDefaultAsync(t => t.Id == request.AgreementTypeId, ct)
            ?? throw new InvalidOperationException("Agreement type not found.");

        if (agreementType.Name == AgreementTypeNames.Support)
        {
            var trained = await _systemProducts.HasCompletedTrainingAsync(request.SystemProductId, ct);
            if (!trained)
                throw new InvalidOperationException("This system/product's training hasn't been marked Completed yet. Training must be completed before a Support agreement can be signed for it.");
        }

        var expiry = request.ExpiryDate ?? request.SignDate.AddYears(1);

        var agreement = new Agreement
        {
            SystemProductId = request.SystemProductId,
            AgreementTypeId = request.AgreementTypeId,
            // A scanned file is attached later via UploadScannedFileAsync, not at creation —
            // any client-provided value here is ignored to keep this null until a real
            // upload happens.
            ScannedFileUrl = null,
            DocumentNumber = await _referenceNumbers.GenerateAgreementDocumentNumberAsync(ct),
            AgreementPlace = request.AgreementPlace,
            SignDate = request.SignDate,
            ExpiryDate = expiry,
            SupportWindowMonths = request.SupportWindowMonths,
            BillingTier = request.BillingTier,
            Details = request.Details,
        };
        _db.Add(agreement);
        await _db.SaveChangesAsync(ct);

        agreement.SystemProduct = systemProduct;
        agreement.AgreementType = agreementType;
        return ToDto(agreement);
    }

    public async Task<IReadOnlyList<AgreementDto>> GetAllAsync(CancellationToken ct = default) =>
        (await AgreementQuery().ToListAsync(ct)).Select(ToDto).ToList();

    public async Task<PagedResult<AgreementDto>> GetAllPagedAsync(AgreementListQuery query, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var filtered = _db.Agreements.AsQueryable();
        switch ((query.Expiry ?? "all").Trim().ToLowerInvariant())
        {
            case "active":
                filtered = filtered.Where(a => a.ExpiryDate >= today);
                break;
            case "expired":
                filtered = filtered.Where(a => a.ExpiryDate < today);
                // "Expired for at least N years": expiry on or before today minus N years.
                if (query.MinYearsExpired is int years && years > 0)
                {
                    var cutoff = today.AddYears(-years);
                    filtered = filtered.Where(a => a.ExpiryDate <= cutoff);
                }
                break;
        }

        var totalCount = await filtered.CountAsync(ct);

        var agreements = await AgreementQuery(filtered)
            .OrderByDescending(a => a.ExpiryDate)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PagedResult<AgreementDto>(agreements.Select(ToDto).ToList(), query.Page, query.PageSize, totalCount);
    }

    public async Task<IReadOnlyList<AgreementDto>> GetForClientAsync(Guid clientId, CancellationToken ct = default) =>
        (await AgreementQuery().Where(a => a.SystemProduct.ClientId == clientId).ToListAsync(ct)).Select(ToDto).ToList();

    public async Task<IReadOnlyList<AgreementDto>> GetForSystemProductAsync(Guid systemProductId, CancellationToken ct = default) =>
        (await AgreementQuery().Where(a => a.SystemProductId == systemProductId).ToListAsync(ct)).Select(ToDto).ToList();

    public async Task<IReadOnlyList<AgreementDto>> GetExpiringSoonAsync(CancellationToken ct = default)
    {
        var in30 = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
        return (await AgreementQuery().Where(a => a.ExpiryDate <= in30).ToListAsync(ct)).Select(ToDto).ToList();
    }

    public async Task<AgreementDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var agreement = await AgreementQuery().FirstOrDefaultAsync(a => a.Id == id, ct);
        return agreement is null ? null : ToDto(agreement);
    }

    public async Task<AgreementDto> UploadScannedFileAsync(Guid agreementId, Stream content, string fileName, string contentType, CancellationToken ct = default)
    {
        var agreement = await AgreementQuery().FirstOrDefaultAsync(a => a.Id == agreementId, ct)
            ?? throw new InvalidOperationException("Agreement not found.");

        var previousStorageKey = agreement.ScannedFileUrl;

        var result = await _storage.SaveAsync(content, fileName, contentType, ct);

        agreement.ScannedFileUrl = result.StorageKey;
        _db.Update(agreement);
        await _db.SaveChangesAsync(ct);

        // Only delete the old file after the new one and the DB update both
        // succeeded — otherwise a failed upload would silently orphan the
        // agreement with no file at all.
        if (!string.IsNullOrEmpty(previousStorageKey))
            await _storage.DeleteAsync(previousStorageKey, ct);

        return ToDto(agreement);
    }

    /// <summary>
    /// Streams the agreement's scanned file. Distinguishes "no scan was
    /// ever uploaded for this agreement" (NoFileAttached) from "one was
    /// uploaded and the storage backend has since lost it" (FileLost) —
    /// collapsing both into a null, as this used to, made a genuinely
    /// lost file indistinguishable from one that was simply never
    /// attached, which read to the person seeing it as unexplained data
    /// loss even when nothing had actually gone missing.
    /// </summary>
    public async Task<FileRetrievalResult> DownloadScannedFileAsync(Guid agreementId, CancellationToken ct = default)
    {
        var agreement = await _db.Agreements.AsNoTracking().FirstOrDefaultAsync(a => a.Id == agreementId, ct);
        if (agreement is null || string.IsNullOrEmpty(agreement.ScannedFileUrl))
            return FileRetrievalResult.NoFile();

        var file = await _storage.GetAsync(agreement.ScannedFileUrl, ct);
        if (file is null)
        {
            _logger.LogWarning(
                "Agreement {AgreementId} has ScannedFileUrl {StorageKey} on record, but the storage backend could not find it.",
                agreementId, agreement.ScannedFileUrl);
            return FileRetrievalResult.Lost();
        }

        return FileRetrievalResult.Found(file);
    }

    /// <summary>
    /// Whether the given caller may download this agreement's scanned
    /// file: any Employee, or the Client the agreement's SystemProduct
    /// belongs to. Distinguishes RecordNotFound (→ 404) from Forbidden
    /// (→ 403 via ForbidOwnership) so a client trying to view someone
    /// else's agreement isn't told the agreement doesn't exist.
    /// </summary>
    public async Task<AttachmentAccessResult> CanAccessScannedFileAsync(
        Guid agreementId, SessionAccountType callerType, Guid callerId, CancellationToken ct = default)
    {
        if (callerType != SessionAccountType.Client)
            return AttachmentAccessResult.Granted;

        var agreement = await _db.Agreements.AsNoTracking()
            .Include(a => a.SystemProduct)
            .FirstOrDefaultAsync(a => a.Id == agreementId, ct);

        if (agreement is null)
            return AttachmentAccessResult.RecordNotFound;

        return agreement.SystemProduct.ClientId == callerId
            ? AttachmentAccessResult.Granted
            : AttachmentAccessResult.Forbidden;
    }

    private IQueryable<Agreement> AgreementQuery(IQueryable<Agreement>? source = null) =>
        (source ?? _db.Agreements).AsNoTracking()
            .Include(a => a.SystemProduct).ThenInclude(s => s.Client)
            .Include(a => a.AgreementType);

    private static AgreementDto ToDto(Agreement a)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var expired = a.ExpiryDate < today;

        // Whole years since expiry: 0 until a full year has passed.
        var years = 0;
        if (expired)
        {
            while (a.ExpiryDate.AddYears(years + 1) <= today) years++;
        }

        return new AgreementDto(
            a.Id, a.SystemProductId, a.SystemProduct.ClientId, a.SystemProduct.Client.Name, a.SystemProduct.Name,
            a.AgreementTypeId, a.AgreementType.Name,
            a.DocumentNumber, a.ScannedFileUrl, a.AgreementPlace,
            a.SignDate, a.ExpiryDate, a.SupportWindowMonths,
            // The stored Status is never advanced automatically, so derive "Expired" from the date.
            expired ? AgreementStatus.Expired : a.Status,
            a.BillingTier, a.Details, expired, years);
    }

    public async Task<AgreementDto> DeleteScannedFileAsync(Guid agreementId, CancellationToken ct = default)
    {
        var agreement = await _db.Agreements
            .Include(a => a.SystemProduct).ThenInclude(s => s.Client)
            .Include(a => a.AgreementType)
            .FirstOrDefaultAsync(a => a.Id == agreementId, ct)
            ?? throw new InvalidOperationException("Agreement not found.");

        var storageKey = agreement.ScannedFileUrl;
        if (string.IsNullOrEmpty(storageKey))
            return ToDto(agreement);

        // Clear the reference first, then delete the file: if the storage
        // delete fails we are left with an orphan blob, never an agreement
        // pointing at a missing file.
        agreement.ScannedFileUrl = null;
        await _db.SaveChangesAsync(ct);

        try { await _storage.DeleteAsync(storageKey, ct); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Agreement {AgreementId}: scanned file {StorageKey} could not be deleted from storage; the reference was already cleared.", agreementId, storageKey);
        }

        return ToDto(agreement);
    }
}
