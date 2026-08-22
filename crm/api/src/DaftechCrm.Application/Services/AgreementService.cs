using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DaftechCrm.Application.Services;

public class AgreementService : IAgreementService
{
    /// <summary>Settings registry key for how many Trainers are auto-assigned per new Training agreement — see SystemConfigurationService.</summary>
    public const string TrainersPerSessionSettingKey = "Training.TrainersPerSession";

    private readonly IAppDbContext _db;
    private readonly IFileStorageService _storage;
    private readonly ReferenceNumberService _referenceNumbers;
    private readonly ITrainerWorkloadService _trainerWorkload;
    private readonly ISystemConfigurationService _config;

    public AgreementService(
        IAppDbContext db, IFileStorageService storage, ReferenceNumberService referenceNumbers,
        ITrainerWorkloadService trainerWorkload, ISystemConfigurationService config)
    {
        _db = db;
        _storage = storage;
        _referenceNumbers = referenceNumbers;
        _trainerWorkload = trainerWorkload;
        _config = config;
    }

    /// <summary>A system/product's training is "complete" once it has a Training agreement whose TrainingSession has reached CompletionStatus.Completed — which itself only happens once every TrainingAssignment on that session is Approved (see ReviewTrainingAssignmentAsync).</summary>
    public async Task<bool> SystemProductHasCompletedTrainingAsync(Guid systemProductId, CancellationToken ct = default) =>
        await _db.Agreements
            .Where(a => a.SystemProductId == systemProductId && a.AgreementType.Name == AgreementTypeNames.Training)
            .Join(_db.TrainingSessions, a => a.Id, t => t.AgreementId, (a, t) => t)
            .AnyAsync(t => t.CompletionStatus == TrainingCompletionStatus.Completed, ct);

    /// <summary>
    /// Creates (signs) a new agreement — always an insert, never overwrites
    /// or updates an existing agreement, even a prior one for the same
    /// SystemProduct/AgreementType. If the resolved AgreementType is
    /// Support, requires the same SystemProduct to already have a
    /// completed Training agreement (see SystemProductHasCompletedTrainingAsync) —
    /// training must finish before support can be signed, per system/product,
    /// not client-wide. A Training-type agreement gets an empty TrainingSession
    /// row created alongside it, and is immediately auto-assigned up to
    /// Training.TrainersPerSession Trainers by current workload (see
    /// ITrainerWorkloadService.SelectTrainersForAssignmentAsync) — an Admin
    /// can still add or remove individual trainers afterward.
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
                throw new InvalidOperationException("This system/product has no completed training yet. A Training agreement must finish (every assigned Trainer's work approved) before a Support agreement can be signed for it.");
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
            var session = new TrainingSession { AgreementId = agreement.Id };
            _db.Add(session);
            await _db.SaveChangesAsync(ct);

            var trainersPerSession = await _config.GetIntAsync(TrainersPerSessionSettingKey, ct);
            var selected = await _trainerWorkload.SelectTrainersForAssignmentAsync(trainersPerSession, ct);
            foreach (var trainerId in selected)
            {
                _db.Add(new TrainingAssignment { TrainingSessionId = session.AgreementId, TrainerEmployeeId = trainerId });
            }
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
            .Include(t => t.TrainerAssignments).ThenInclude(a => a.TrainerEmployee)
            .FirstOrDefaultAsync(t => t.AgreementId == agreementId, ct);
        return session is null ? null : ToTrainingSessionDto(session);
    }

    /// <summary>Updates the non-trainer fields of a TrainingSession for a Training-type agreement. Throws if the agreement isn't a Training agreement (no TrainingSession row exists for it — see CreateAsync, which always creates one alongside a Training agreement).</summary>
    public async Task<TrainingSessionDto> SaveTrainingSessionAsync(Guid agreementId, SaveTrainingSessionRequest request, CancellationToken ct = default)
    {
        var session = await _db.TrainingSessions.Include(t => t.TrainerAssignments).ThenInclude(a => a.TrainerEmployee)
            .FirstOrDefaultAsync(t => t.AgreementId == agreementId, ct)
            ?? throw new InvalidOperationException("This agreement has no training session — it isn't a Training-type agreement.");

        session.StartDate = request.StartDate;
        // EndDate/CompletionStatus are otherwise system-derived (see
        // ReviewTrainingAssignmentAsync) — but an Admin can still push
        // EndDate out manually (e.g. training genuinely ran long past what
        // approvals alone captured), matching the old field's documented
        // "stays editable afterward" behavior. Only accepted here if the
        // session has already reached Completed, so this can't be used to
        // fabricate a completion the assignments haven't actually earned.
        if (session.CompletionStatus == TrainingCompletionStatus.Completed)
            session.EndDate = request.EndDate;
        session.Location = request.Location;
        session.Participants = request.Participants;
        session.Attendance = request.Attendance;
        session.TopicsCovered = request.TopicsCovered;
        session.IssuesOrQuestions = request.IssuesOrQuestions;
        session.TrainerComments = request.TrainerComments;
        session.ClientRepresentativeConfirmation = request.ClientRepresentativeConfirmation;
        session.ClientRepresentativeComments = request.ClientRepresentativeComments;
        session.FollowUpRequired = request.FollowUpRequired;
        session.FollowUpNotes = request.FollowUpNotes;

        _db.Update(session);
        await _db.SaveChangesAsync(ct);

        return ToTrainingSessionDto(session);
    }

    /// <summary>Manually adds a Trainer to a session's roster, alongside whatever auto-assignment already placed there.</summary>
    public async Task<TrainingSessionDto> AddTrainingAssignmentAsync(Guid agreementId, Guid trainerEmployeeId, CancellationToken ct = default)
    {
        var session = await _db.TrainingSessions.Include(t => t.TrainerAssignments).ThenInclude(a => a.TrainerEmployee)
            .FirstOrDefaultAsync(t => t.AgreementId == agreementId, ct)
            ?? throw new InvalidOperationException("This agreement has no training session — it isn't a Training-type agreement.");

        if (session.TrainerAssignments.Any(a => a.TrainerEmployeeId == trainerEmployeeId))
            throw new InvalidOperationException("This employee is already assigned to this training session.");

        var trainer = await _db.Employees.FirstOrDefaultAsync(e => e.Id == trainerEmployeeId && !e.IsDeleted, ct)
            ?? throw new InvalidOperationException("Trainer not found.");
        if (!trainer.Roles.Contains(Domain.Enums.EmployeeRole.Trainer))
            throw new InvalidOperationException("This employee does not have the Trainer responsibility assigned.");

        _db.Add(new TrainingAssignment { TrainingSessionId = session.AgreementId, TrainerEmployeeId = trainerEmployeeId });
        await _db.SaveChangesAsync(ct);

        return (await GetTrainingSessionAsync(agreementId, ct))!;
    }

    /// <summary>Removes a Trainer from a session's roster. Blocked once that trainer's assignment is Approved — that's a finished part of the training record, not a roster mistake to undo.</summary>
    public async Task<TrainingSessionDto> RemoveTrainingAssignmentAsync(Guid agreementId, Guid assignmentId, CancellationToken ct = default)
    {
        var assignment = await _db.TrainingAssignments.FirstOrDefaultAsync(a => a.Id == assignmentId && a.TrainingSessionId == agreementId, ct)
            ?? throw new InvalidOperationException("Training assignment not found.");

        if (assignment.Status == TrainingAssignmentStatus.Approved)
            throw new InvalidOperationException("This assignment has already been approved and is part of the completed training record — it can't be removed.");

        _db.Remove(assignment);
        await _db.SaveChangesAsync(ct);

        return (await GetTrainingSessionAsync(agreementId, ct))!;
    }

    /// <summary>The trainer submits their own work for Admin review. Only the assigned trainer may call this for their own assignment — enforced by callerEmployeeId, resolved server-side from the caller's JWT, never trusted from the request body.</summary>
    public async Task<TrainingAssignmentDto> SubmitTrainingAssignmentAsync(Guid assignmentId, Guid callerEmployeeId, SubmitTrainingAssignmentRequest request, CancellationToken ct = default)
    {
        var assignment = await _db.TrainingAssignments.Include(a => a.TrainerEmployee)
            .FirstOrDefaultAsync(a => a.Id == assignmentId, ct)
            ?? throw new InvalidOperationException("Training assignment not found.");

        if (assignment.TrainerEmployeeId != callerEmployeeId)
            throw new InvalidOperationException("You can only submit your own training assignment.");

        if (assignment.Status is TrainingAssignmentStatus.Submitted or TrainingAssignmentStatus.Approved)
            throw new InvalidOperationException("This assignment has already been submitted for review.");

        if (string.IsNullOrWhiteSpace(request.WorkDescription))
            throw new InvalidOperationException("A description of the completed work is required before submitting.");

        assignment.WorkDescription = request.WorkDescription;
        assignment.Status = TrainingAssignmentStatus.Submitted;
        assignment.SubmittedAt = DateTimeOffset.UtcNow;
        // A fresh submission clears any prior rejection note — it no longer
        // describes the state of the (now-revised) work.
        assignment.ReviewNotes = null;
        assignment.ReviewedByName = null;
        assignment.ReviewedAt = null;

        _db.Update(assignment);
        await _db.SaveChangesAsync(ct);

        return ToTrainingAssignmentDto(assignment);
    }

    /// <summary>Uploads (or replaces) the trainer's own evidence file. Same caller-ownership check as SubmitTrainingAssignmentAsync.</summary>
    public async Task<TrainingAssignmentDto> UploadTrainingAssignmentFileAsync(Guid assignmentId, Guid callerEmployeeId, Stream content, string fileName, string contentType, CancellationToken ct = default)
    {
        var assignment = await _db.TrainingAssignments.Include(a => a.TrainerEmployee)
            .FirstOrDefaultAsync(a => a.Id == assignmentId, ct)
            ?? throw new InvalidOperationException("Training assignment not found.");

        if (assignment.TrainerEmployeeId != callerEmployeeId)
            throw new InvalidOperationException("You can only upload a file to your own training assignment.");

        var previousStorageKey = assignment.FileStorageKey;

        var result = await _storage.SaveAsync(content, fileName, contentType, ct);

        assignment.FileStorageKey = result.StorageKey;
        assignment.FileName = result.OriginalFileName;
        _db.Update(assignment);
        await _db.SaveChangesAsync(ct);

        if (!string.IsNullOrEmpty(previousStorageKey))
            await _storage.DeleteAsync(previousStorageKey, ct);

        return ToTrainingAssignmentDto(assignment);
    }

    public async Task<RetrievedFile?> DownloadTrainingAssignmentFileAsync(Guid assignmentId, CancellationToken ct = default)
    {
        var assignment = await _db.TrainingAssignments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == assignmentId, ct);
        if (assignment is null || string.IsNullOrEmpty(assignment.FileStorageKey))
            return null;

        return await _storage.GetAsync(assignment.FileStorageKey, ct);
    }

    /// <summary>
    /// Admin approves or rejects a Submitted assignment. On approval, if
    /// every assignment on the session is now Approved, the session itself
    /// is advanced to CompletionStatus.Completed with EndDate set to today
    /// (if not already set) — this is the sole path by which a session
    /// reaches Completed, which is what SystemProductHasCompletedTrainingAsync
    /// checks before a Support agreement can be signed. Rejecting sets the
    /// assignment back to RejectedNeedsRework so the trainer can revise and
    /// resubmit; the session's own status is left untouched either way if
    /// not every assignment is Approved yet.
    /// </summary>
    public async Task<TrainingSessionDto> ReviewTrainingAssignmentAsync(Guid assignmentId, ReviewTrainingAssignmentRequest request, string reviewedByName, CancellationToken ct = default)
    {
        var assignment = await _db.TrainingAssignments
            .FirstOrDefaultAsync(a => a.Id == assignmentId, ct)
            ?? throw new InvalidOperationException("Training assignment not found.");

        if (assignment.Status != TrainingAssignmentStatus.Submitted)
            throw new InvalidOperationException("Only a submitted assignment can be reviewed.");

        if (!request.Approve && string.IsNullOrWhiteSpace(request.ReviewNotes))
            throw new InvalidOperationException("Review notes are required when rejecting an assignment, so the trainer knows what to revise.");

        assignment.Status = request.Approve ? TrainingAssignmentStatus.Approved : TrainingAssignmentStatus.RejectedNeedsRework;
        assignment.ReviewedByName = reviewedByName;
        assignment.ReviewedAt = DateTimeOffset.UtcNow;
        assignment.ReviewNotes = request.ReviewNotes;
        _db.Update(assignment);
        await _db.SaveChangesAsync(ct);

        var session = await _db.TrainingSessions.Include(t => t.TrainerAssignments).ThenInclude(a => a.TrainerEmployee)
            .FirstAsync(t => t.AgreementId == assignment.TrainingSessionId, ct);

        // Every assignment approved (and there's at least one — an empty
        // roster is never "complete") is what promotes the session itself.
        if (session.TrainerAssignments.Count > 0 && session.TrainerAssignments.All(a => a.Status == TrainingAssignmentStatus.Approved))
        {
            session.CompletionStatus = TrainingCompletionStatus.Completed;
            session.EndDate ??= DateOnly.FromDateTime(DateTime.UtcNow);
            _db.Update(session);
            await _db.SaveChangesAsync(ct);
        }
        else if (session.CompletionStatus == TrainingCompletionStatus.NotStarted)
        {
            // At least one assignment has moved (submitted/reviewed) — the
            // session is no longer untouched, even though it isn't fully
            // approved yet.
            session.CompletionStatus = TrainingCompletionStatus.InProgress;
            _db.Update(session);
            await _db.SaveChangesAsync(ct);
        }

        return ToTrainingSessionDto(session);
    }

    public async Task<IReadOnlyList<TrainingAssignmentDto>> GetAssignmentsForTrainerAsync(Guid trainerEmployeeId, CancellationToken ct = default)
    {
        var assignments = await _db.TrainingAssignments.AsNoTracking()
            .Include(a => a.TrainerEmployee)
            .Where(a => a.TrainerEmployeeId == trainerEmployeeId)
            .OrderByDescending(a => a.AssignedAt)
            .ToListAsync(ct);

        return assignments.Select(ToTrainingAssignmentDto).ToList();
    }

    public async Task<TrainingSessionDto> UploadTrainingScanAsync(Guid agreementId, Stream content, string fileName, string contentType, CancellationToken ct = default)
    {
        var session = await _db.TrainingSessions.Include(t => t.TrainerAssignments).ThenInclude(a => a.TrainerEmployee)
            .FirstOrDefaultAsync(t => t.AgreementId == agreementId, ct)
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
        var sessions = await _db.TrainingSessions.AsNoTracking()
            .Include(t => t.TrainerAssignments).ThenInclude(a => a.TrainerEmployee)
            .Where(t => agreementIds.Contains(t.AgreementId)).ToListAsync(ct);
        var sessionsByAgreement = sessions.ToDictionary(t => t.AgreementId);

        return agreements.Select(a => ToDto(a, sessionsByAgreement.GetValueOrDefault(a.Id))).ToList();
    }

    private async Task<AgreementDto> ToDtoAsync(Agreement a, CancellationToken ct)
    {
        var session = await _db.TrainingSessions.AsNoTracking()
            .Include(t => t.TrainerAssignments).ThenInclude(assn => assn.TrainerEmployee)
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
        t.AgreementId, t.TrainerAssignments.OrderBy(a => a.AssignedAt).Select(ToTrainingAssignmentDto).ToList(),
        t.StartDate, t.EndDate, t.Location, t.Participants, t.Attendance,
        t.TopicsCovered, t.IssuesOrQuestions, t.TrainerComments,
        t.ClientRepresentativeConfirmation, t.ClientRepresentativeComments,
        t.CompletionStatus, t.FollowUpRequired, t.FollowUpNotes, t.ScanFileName
    );

    private static TrainingAssignmentDto ToTrainingAssignmentDto(TrainingAssignment a) => new(
        a.Id, a.TrainingSessionId, a.TrainerEmployeeId, a.TrainerEmployee?.FullName ?? "(unknown)",
        a.AssignedAt, a.WorkDescription, a.FileName,
        a.Status, a.SubmittedAt, a.ReviewedByName, a.ReviewedAt, a.ReviewNotes
    );
}
