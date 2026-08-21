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

    public async Task<bool> SystemProductHasCompletedTrainingAsync(Guid systemProductId, CancellationToken ct = default) =>
        await _db.Agreements
            .Where(a => a.SystemProductId == systemProductId && a.AgreementType.Name == AgreementTypeNames.Training)
            .Join(_db.TrainingSessions, a => a.Id, t => t.AgreementId, (a, t) => t)
            .AnyAsync(t => t.EndDate.HasValue, ct);

    /// <summary>
    /// Creates (signs) a new agreement — always an insert, never overwrites
    /// or updates an existing agreement, even a prior one for the same
    /// SystemProduct/AgreementType. If the resolved AgreementType is
    /// Support, requires the same SystemProduct to already have a
    /// completed Training agreement (see SystemProductHasCompletedTrainingAsync) —
    /// training must finish before support can be signed, per system/product,
    /// not client-wide. A Training-type agreement gets an empty TrainingSession
    /// row created alongside it, ready to be filled in via SaveTrainingSessionAsync.
    /// </summary>
    public async Task<AgreementDto> CreateAsync(CreateAgreementRequest request, CancellationToken ct = default)
    {
        var systemProduct = await _db.SystemProducts.Include(s => s.Client)
            .FirstOrDefaultAsync(s => s.Id == request.SystemProductId && !s.IsDeleted, ct)
            ?? throw new InvalidOperationException("System/Product not found.");

        var agreementType = await _db.AgreementTypes.FirstOrDefaultAsync(t => t.Id == request.AgreementTypeId, ct)
            ?? throw new InvalidOperationException("Agreement type not found.");

        if (agreementType.Name == AgreementTypeNames.Support)
        {
            var trained = await SystemProductHasCompletedTrainingAsync(request.SystemProductId, ct);
            if (!trained)
                throw new InvalidOperationException("This system/product has no completed training yet. A Training agreement must finish (an End Date must be set) before a Support agreement can be signed for it.");
        }

        var expiry = request.ExpiryDate ?? request.SignDate.AddYears(1);

        var agreement = new Agreement
        {
            SystemProductId = request.SystemProductId,
            AgreementTypeId = request.AgreementTypeId,
            DocumentNumber = await _referenceNumbers.GenerateAgreementDocumentNumberAsync(ct),
            // A scanned file is attached later via UploadScannedFileAsync, not at creation —
            // any client-provided value here is ignored to keep this null until a real
            // upload happens.
            ScannedFileUrl = null,
            AgreementPlace = request.AgreementPlace,
            SignDate = request.SignDate,
            ExpiryDate = expiry,
            SupportWindowMonths = request.SupportWindowMonths,
            BillingTier = request.BillingTier,
            Details = request.Details,
        };
        _db.Add(agreement);

        if (agreementType.Name == AgreementTypeNames.Training)
        {
            _db.Add(new TrainingSession { AgreementId = agreement.Id });
        }

        await _db.SaveChangesAsync(ct);

        agreement.SystemProduct = systemProduct;
        agreement.AgreementType = agreementType;
        return await ToDtoAsync(agreement, ct);
    }

    public async Task<IReadOnlyList<AgreementDto>> GetAllAsync(CancellationToken ct = default)
    {
        var agreements = await AgreementQuery().ToListAsync(ct);
        return await ToDtosAsync(agreements, ct);
    }

    public async Task<PagedResult<AgreementDto>> GetAllPagedAsync(PaginationQuery query, CancellationToken ct = default)
    {
        var totalCount = await _db.Agreements.CountAsync(ct);

        var agreements = await AgreementQuery()
            .OrderByDescending(a => a.ExpiryDate)
            .Skip(query.Skip)
            .Take(query.PageSize)
            .ToListAsync(ct);

        var dtos = await ToDtosAsync(agreements, ct);
        return new PagedResult<AgreementDto>(dtos, query.Page, query.PageSize, totalCount);
    }

    public async Task<IReadOnlyList<AgreementDto>> GetForClientAsync(Guid clientId, CancellationToken ct = default)
    {
        var agreements = await AgreementQuery().Where(a => a.SystemProduct.ClientId == clientId).ToListAsync(ct);
        return await ToDtosAsync(agreements, ct);
    }

    public async Task<IReadOnlyList<AgreementDto>> GetForSystemProductAsync(Guid systemProductId, CancellationToken ct = default)
    {
        var agreements = await AgreementQuery().Where(a => a.SystemProductId == systemProductId).ToListAsync(ct);
        return await ToDtosAsync(agreements, ct);
    }

    public async Task<IReadOnlyList<AgreementDto>> GetExpiringSoonAsync(CancellationToken ct = default)
    {
        var in30 = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
        var agreements = await AgreementQuery().Where(a => a.ExpiryDate <= in30).ToListAsync(ct);
        return await ToDtosAsync(agreements, ct);
    }

    public async Task<AgreementDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var agreement = await AgreementQuery().FirstOrDefaultAsync(a => a.Id == id, ct);
        return agreement is null ? null : await ToDtoAsync(agreement, ct);
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

        return await ToDtoAsync(agreement, ct);
    }

    public async Task<RetrievedFile?> DownloadScannedFileAsync(Guid agreementId, CancellationToken ct = default)
    {
        var agreement = await _db.Agreements.AsNoTracking().FirstOrDefaultAsync(a => a.Id == agreementId, ct);
        if (agreement is null || string.IsNullOrEmpty(agreement.ScannedFileUrl))
            return null;

        return await _storage.GetAsync(agreement.ScannedFileUrl, ct);
    }

    public async Task<TrainingSessionDto?> GetTrainingSessionAsync(Guid agreementId, CancellationToken ct = default)
    {
        var session = await _db.TrainingSessions.AsNoTracking()
            .Include(t => t.TrainerEmployee)
            .FirstOrDefaultAsync(t => t.AgreementId == agreementId, ct);
        return session is null ? null : ToTrainingSessionDto(session);
    }

    /// <summary>Updates the TrainingSession fields for a Training-type agreement. Throws if the agreement isn't a Training agreement (no TrainingSession row exists for it — see CreateAsync, which always creates one alongside a Training agreement).</summary>
    public async Task<TrainingSessionDto> SaveTrainingSessionAsync(Guid agreementId, SaveTrainingSessionRequest request, CancellationToken ct = default)
    {
        var session = await _db.TrainingSessions.Include(t => t.TrainerEmployee).FirstOrDefaultAsync(t => t.AgreementId == agreementId, ct)
            ?? throw new InvalidOperationException("This agreement has no training session — it isn't a Training-type agreement.");

        if (request.TrainerEmployeeId.HasValue)
        {
            var trainer = await _db.Employees.FirstOrDefaultAsync(e => e.Id == request.TrainerEmployeeId.Value && !e.IsDeleted, ct)
                ?? throw new InvalidOperationException("Trainer not found.");
            if (!trainer.Roles.Contains(Domain.Enums.EmployeeRole.Trainer))
                throw new InvalidOperationException("This employee does not have the Trainer responsibility assigned.");
        }

        session.TrainerEmployeeId = request.TrainerEmployeeId;
        session.StartDate = request.StartDate;
        session.EndDate = request.EndDate;
        session.Location = request.Location;
        session.Participants = request.Participants;
        session.Attendance = request.Attendance;
        session.TopicsCovered = request.TopicsCovered;
        session.IssuesOrQuestions = request.IssuesOrQuestions;
        session.TrainerComments = request.TrainerComments;
        session.ClientRepresentativeConfirmation = request.ClientRepresentativeConfirmation;
        session.ClientRepresentativeComments = request.ClientRepresentativeComments;
        session.CompletionStatus = request.CompletionStatus;
        session.FollowUpRequired = request.FollowUpRequired;
        session.FollowUpNotes = request.FollowUpNotes;

        _db.Update(session);
        await _db.SaveChangesAsync(ct);

        // The trainer navigation may be stale after changing TrainerEmployeeId — reload for an accurate DTO.
        session.TrainerEmployee = request.TrainerEmployeeId.HasValue
            ? await _db.Employees.AsNoTracking().FirstOrDefaultAsync(e => e.Id == request.TrainerEmployeeId.Value, ct)
            : null;

        return ToTrainingSessionDto(session);
    }

    public async Task<TrainingSessionDto> UploadTrainingScanAsync(Guid agreementId, Stream content, string fileName, string contentType, CancellationToken ct = default)
    {
        var session = await _db.TrainingSessions.Include(t => t.TrainerEmployee).FirstOrDefaultAsync(t => t.AgreementId == agreementId, ct)
            ?? throw new InvalidOperationException("This agreement has no training session — it isn't a Training-type agreement.");

        var previousStorageKey = session.ScanStorageKey;

        var result = await _storage.SaveAsync(content, fileName, contentType, ct);

        session.ScanStorageKey = result.StorageKey;
        session.ScanFileName = result.OriginalFileName;
        _db.Update(session);
        await _db.SaveChangesAsync(ct);

        if (!string.IsNullOrEmpty(previousStorageKey))
            await _storage.DeleteAsync(previousStorageKey, ct);

        return ToTrainingSessionDto(session);
    }

    public async Task<RetrievedFile?> DownloadTrainingScanAsync(Guid agreementId, CancellationToken ct = default)
    {
        var session = await _db.TrainingSessions.AsNoTracking().FirstOrDefaultAsync(t => t.AgreementId == agreementId, ct);
        if (session is null || string.IsNullOrEmpty(session.ScanStorageKey))
            return null;

        return await _storage.GetAsync(session.ScanStorageKey, ct);
    }

    private IQueryable<Agreement> AgreementQuery() =>
        _db.Agreements.AsNoTracking()
            .Include(a => a.SystemProduct).ThenInclude(s => s.Client)
            .Include(a => a.AgreementType);

    private async Task<IReadOnlyList<AgreementDto>> ToDtosAsync(IReadOnlyList<Agreement> agreements, CancellationToken ct)
    {
        var agreementIds = agreements.Select(a => a.Id).ToList();
        var sessions = await _db.TrainingSessions.AsNoTracking().Include(t => t.TrainerEmployee)
            .Where(t => agreementIds.Contains(t.AgreementId)).ToListAsync(ct);
        var sessionsByAgreement = sessions.ToDictionary(t => t.AgreementId);

        return agreements.Select(a => ToDto(a, sessionsByAgreement.GetValueOrDefault(a.Id))).ToList();
    }

    private async Task<AgreementDto> ToDtoAsync(Agreement a, CancellationToken ct)
    {
        var session = await _db.TrainingSessions.AsNoTracking().Include(t => t.TrainerEmployee)
            .FirstOrDefaultAsync(t => t.AgreementId == a.Id, ct);
        return ToDto(a, session);
    }

    private static AgreementDto ToDto(Agreement a, TrainingSession? session) => new(
        a.Id, a.SystemProductId, a.SystemProduct.ClientId, a.SystemProduct.Client.Name, a.SystemProduct.Name,
        a.AgreementTypeId, a.AgreementType.Name,
        a.DocumentNumber, a.ScannedFileUrl, a.AgreementPlace,
        a.SignDate, a.ExpiryDate, a.SupportWindowMonths, a.Status, a.BillingTier,
        a.Details, session is null ? null : ToTrainingSessionDto(session)
    );

    private static TrainingSessionDto ToTrainingSessionDto(TrainingSession t) => new(
        t.AgreementId, t.TrainerEmployeeId, t.TrainerEmployee?.FullName,
        t.StartDate, t.EndDate, t.Location, t.Participants, t.Attendance,
        t.TopicsCovered, t.IssuesOrQuestions, t.TrainerComments,
        t.ClientRepresentativeConfirmation, t.ClientRepresentativeComments,
        t.CompletionStatus, t.FollowUpRequired, t.FollowUpNotes, t.ScanFileName
    );
}
