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

    public AgreementService(IAppDbContext db, IFileStorageService storage, ReferenceNumberService referenceNumbers)
    {
        _db = db;
        _storage = storage;
        _referenceNumbers = referenceNumbers;
    }

    public async Task<bool> ClientHasCompletedTrainingAsync(Guid clientId, CancellationToken ct = default) =>
        await _db.AgreementTrainings.AnyAsync(t => t.ClientId == clientId && t.EndDate.HasValue, ct);

    /// <summary>
    /// Creating the Agreement IS the admin's act of signing it: SignDate is
    /// always set to today, never accepted from the caller. Training is
    /// mandatory and must finish first — this throws if the client has no
    /// training with an EndDate yet, so a support agreement can never be
    /// signed ahead of training (which was the source of client-side
    /// complaints about staff not being trained on the system before
    /// support began). Any of the client's completed trainings are linked
    /// to the new agreement for record-keeping.
    /// </summary>
    public async Task<AgreementDto> CreateAsync(CreateAgreementRequest request, CancellationToken ct = default)
    {
        var completedTrainings = await _db.AgreementTrainings
            .Where(t => t.ClientId == request.ClientId && t.EndDate.HasValue)
            .ToListAsync(ct);

        if (completedTrainings.Count == 0)
            throw new InvalidOperationException("This client has no completed training yet. Training must finish (an End Date must be set) before the support agreement can be signed.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var expiry = request.ExpiryDate ?? today.AddYears(1);

        var agreement = new Agreement
        {
            ClientId = request.ClientId,
            DocumentNumber = await _referenceNumbers.GenerateAgreementDocumentNumberAsync(ct),
            // A scanned file is attached later via UploadScannedFileAsync, not at creation —
            // any client-provided value here is ignored to keep this null until a real
            // upload happens (see Final_version_fix.docx item 1: "ensure ScannedFileUrl is null").
            ScannedFileUrl = null,
            AgreementPlace = request.AgreementPlace,
            // The admin creating this agreement is the signing act — always today,
            // never derived and never accepted from the request.
            SignDate = today,
            ExpiryDate = expiry,
            SupportWindowMonths = request.SupportWindowMonths,
            BillingTier = request.BillingTier,
        };
        _db.Add(agreement);

        foreach (var training in completedTrainings)
        {
            training.AgreementId = agreement.Id;
            _db.Update(training);
        }

        await _db.SaveChangesAsync(ct);

        agreement.Trainings = completedTrainings;
        return ToDto(agreement);
    }

    public async Task<IReadOnlyList<AgreementDto>> GetAllAsync(CancellationToken ct = default) =>
        (await _db.Agreements.AsNoTracking().Include(a => a.Trainings).ToListAsync(ct)).Select(ToDto).ToList();

    public async Task<PagedResult<AgreementDto>> GetAllPagedAsync(PaginationQuery query, CancellationToken ct = default)
    {
        var totalCount = await _db.Agreements.CountAsync(ct);

        var items = await _db.Agreements
            .AsNoTracking()
            .Include(a => a.Trainings)
            .OrderByDescending(a => a.ExpiryDate)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PagedResult<AgreementDto>(items.Select(ToDto).ToList(), query.Page, query.PageSize, totalCount);
    }

    public async Task<IReadOnlyList<AgreementDto>> GetForClientAsync(Guid clientId, CancellationToken ct = default) =>
        (await _db.Agreements.AsNoTracking().Include(a => a.Trainings).Where(a => a.ClientId == clientId).ToListAsync(ct)).Select(ToDto).ToList();

    public async Task<IReadOnlyList<AgreementDto>> GetExpiringSoonAsync(CancellationToken ct = default)
    {
        var in30 = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
        return (await _db.Agreements.AsNoTracking().Include(a => a.Trainings).Where(a => a.ExpiryDate <= in30).ToListAsync(ct)).Select(ToDto).ToList();
    }

    public async Task<AgreementDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var agreement = await _db.Agreements.AsNoTracking().Include(a => a.Trainings).FirstOrDefaultAsync(a => a.Id == id, ct);
        return agreement is null ? null : ToDto(agreement);
    }

    public async Task<AgreementDto> UploadScannedFileAsync(Guid agreementId, Stream content, string fileName, string contentType, CancellationToken ct = default)
    {
        var agreement = await _db.Agreements.Include(a => a.Trainings).FirstOrDefaultAsync(a => a.Id == agreementId, ct)
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

    public async Task<IReadOnlyList<AgreementTrainingDto>> GetTrainingsForClientAsync(Guid clientId, CancellationToken ct = default) =>
        (await _db.AgreementTrainings.AsNoTracking().Where(t => t.ClientId == clientId).ToListAsync(ct))
            .Select(ToTrainingDto).ToList();

    /// <summary>Creates a new, empty training row for a client — not attached to any agreement, since training happens before an agreement can even be signed. Details are filled in afterward via SaveTrainingAsync/UploadTrainingScanAsync.</summary>
    public async Task<AgreementTrainingDto> AddTrainingAsync(Guid clientId, CancellationToken ct = default)
    {
        var clientExists = await _db.Clients.AnyAsync(c => c.Id == clientId, ct);
        if (!clientExists)
            throw new InvalidOperationException("Client not found.");

        var training = new AgreementTraining { ClientId = clientId };
        _db.Add(training);
        await _db.SaveChangesAsync(ct);
        return ToTrainingDto(training);
    }

    /// <summary>Sets/updates one training row's description and timeline. EndDate stays editable after being set (e.g. the admin extends it if training runs long due to unforeseen delays) — no separate "completed" flag, EndDate being set is what completion means.</summary>
    public async Task<AgreementTrainingDto> SaveTrainingAsync(Guid clientId, Guid trainingId, SaveAgreementTrainingRequest request, CancellationToken ct = default)
    {
        if (request.Description is { Length: > 1000 })
            throw new ValidationException("Description must be 1000 characters or fewer.");

        var training = await _db.AgreementTrainings.FirstOrDefaultAsync(t => t.Id == trainingId && t.ClientId == clientId, ct)
            ?? throw new InvalidOperationException("Training not found for this client.");

        training.Description = request.Description;
        training.StartDate = request.StartDate;
        training.EndDate = request.EndDate;

        _db.Update(training);
        await _db.SaveChangesAsync(ct);
        return ToTrainingDto(training);
    }

    /// <summary>Deletes a training row (and its scan file, if any).</summary>
    public async Task DeleteTrainingAsync(Guid clientId, Guid trainingId, CancellationToken ct = default)
    {
        var training = await _db.AgreementTrainings.FirstOrDefaultAsync(t => t.Id == trainingId && t.ClientId == clientId, ct)
            ?? throw new InvalidOperationException("Training not found for this client.");

        var storageKey = training.ScanStorageKey;

        _db.Remove(training);
        await _db.SaveChangesAsync(ct);

        if (!string.IsNullOrEmpty(storageKey))
            await _storage.DeleteAsync(storageKey, ct);
    }

    /// <summary>Uploads (or replaces) the scanned document for one specific training row — a separate file from the signed-agreement scan.</summary>
    public async Task<AgreementTrainingDto> UploadTrainingScanAsync(Guid clientId, Guid trainingId, Stream content, string fileName, string contentType, CancellationToken ct = default)
    {
        var training = await _db.AgreementTrainings.FirstOrDefaultAsync(t => t.Id == trainingId && t.ClientId == clientId, ct)
            ?? throw new InvalidOperationException("Training not found for this client.");

        var previousStorageKey = training.ScanStorageKey;

        var result = await _storage.SaveAsync(content, fileName, contentType, ct);

        training.ScanStorageKey = result.StorageKey;
        training.ScanFileName = result.OriginalFileName;
        _db.Update(training);
        await _db.SaveChangesAsync(ct);

        if (!string.IsNullOrEmpty(previousStorageKey))
            await _storage.DeleteAsync(previousStorageKey, ct);

        return ToTrainingDto(training);
    }

    public async Task<RetrievedFile?> DownloadTrainingScanAsync(Guid clientId, Guid trainingId, CancellationToken ct = default)
    {
        var training = await _db.AgreementTrainings.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == trainingId && t.ClientId == clientId, ct);
        if (training is null || string.IsNullOrEmpty(training.ScanStorageKey))
            return null;

        return await _storage.GetAsync(training.ScanStorageKey, ct);
    }

    private static AgreementDto ToDto(Agreement a) => new(
        a.Id, a.ClientId, a.DocumentNumber, a.ScannedFileUrl, a.AgreementPlace,
        a.SignDate, a.ExpiryDate, a.SupportWindowMonths, a.Status, a.BillingTier,
        a.Trainings.Select(ToTrainingDto).ToList()
    );

    private static AgreementTrainingDto ToTrainingDto(AgreementTraining t) => new(
        t.Id, t.ClientId, t.AgreementId, t.Description, t.StartDate, t.EndDate, t.ScanFileName
    );
}
