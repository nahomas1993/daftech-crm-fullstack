using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DaftechCrm.Application.Services;

public class AgreementService : IAgreementService
{
    private readonly IAppDbContext _db;
    private readonly IFileStorageService _storage;
    private readonly ReferenceNumberService _referenceNumbers;
    private readonly ISystemProductService _systemProducts;

    public AgreementService(
        IAppDbContext db, IFileStorageService storage, ReferenceNumberService referenceNumbers,
        ISystemProductService systemProducts)
    {
        _db = db;
        _storage = storage;
        _referenceNumbers = referenceNumbers;
        _systemProducts = systemProducts;
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

    public async Task<PagedResult<AgreementDto>> GetAllPagedAsync(PaginationQuery query, CancellationToken ct = default)
    {
        var totalCount = await _db.Agreements.CountAsync(ct);

        var agreements = await AgreementQuery()
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

    public async Task<RetrievedFile?> DownloadScannedFileAsync(Guid agreementId, CancellationToken ct = default)
    {
        var agreement = await _db.Agreements.AsNoTracking().FirstOrDefaultAsync(a => a.Id == agreementId, ct);
        if (agreement is null || string.IsNullOrEmpty(agreement.ScannedFileUrl))
            return null;

        return await _storage.GetAsync(agreement.ScannedFileUrl, ct);
    }

    private IQueryable<Agreement> AgreementQuery() =>
        _db.Agreements.AsNoTracking()
            .Include(a => a.SystemProduct).ThenInclude(s => s.Client)
            .Include(a => a.AgreementType);

    private static AgreementDto ToDto(Agreement a) => new(
        a.Id, a.SystemProductId, a.SystemProduct.ClientId, a.SystemProduct.Client.Name, a.SystemProduct.Name,
        a.AgreementTypeId, a.AgreementType.Name,
        a.DocumentNumber, a.ScannedFileUrl, a.AgreementPlace,
        a.SignDate, a.ExpiryDate, a.SupportWindowMonths, a.Status, a.BillingTier,
        a.Details
    );
}
