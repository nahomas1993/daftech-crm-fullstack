using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Domain.Entities;
using DaftechCrm.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DaftechCrm.Application.Services;

public class TrainingRecordService : ITrainingRecordService
{
    private readonly IAppDbContext _db;
    private readonly IFileStorageService _storage;

    public TrainingRecordService(IAppDbContext db, IFileStorageService storage)
    {
        _db = db;
        _storage = storage;
    }

    /// <summary>
    /// Logs one training session. callerEmployeeId must currently be on
    /// the target SystemProduct's training roster (see
    /// ISystemProductService.AddTrainingAssignmentAsync/AutoAssignTrainersAsync)
    /// — only an assigned Trainer/Technician may log against it. Always
    /// inserts a new row; repeat as many times as needed for multiple
    /// sessions, even after the system/product's training has already
    /// been marked Completed (e.g. a refresher).
    /// </summary>
    public async Task<TrainingRecordDto> CreateAsync(Guid callerEmployeeId, CreateTrainingRecordRequest request, CancellationToken ct = default)
    {
        var onRoster = await _db.TrainingAssignments
            .AnyAsync(a => a.SystemProductId == request.SystemProductId && a.TrainerEmployeeId == callerEmployeeId, ct);
        if (!onRoster)
            throw new InvalidOperationException("You are not assigned to train on this client's system/product.");

        return await CreateInternalAsync(callerEmployeeId, request, ct);
    }

    /// <summary>
    /// Admin encodes a historical training session on behalf of
    /// trainerEmployeeId — see the ITrainingRecordService doc comment for
    /// when this is used instead of CreateAsync. Deliberately skips the
    /// roster check (the trainer may no longer be assigned to this
    /// system/product, or never formally was, if the session predates the
    /// roster feature entirely), but still requires trainerEmployeeId to
    /// actually hold the Trainer role — this isn't a way to attribute a
    /// session to an arbitrary non-training employee.
    /// </summary>
    public async Task<TrainingRecordDto> AdminCreateAsync(Guid trainerEmployeeId, CreateTrainingRecordRequest request, CancellationToken ct = default)
    {
        var trainerCandidate = await _db.Employees.FirstOrDefaultAsync(e => e.Id == trainerEmployeeId, ct)
            ?? throw new InvalidOperationException("Selected trainer was not found.");
        if (!trainerCandidate.Roles.Contains(EmployeeRole.Trainer))
            throw new InvalidOperationException($"{trainerCandidate.FullName} does not have the Trainer role.");

        return await CreateInternalAsync(trainerEmployeeId, request, ct);
    }

    /// <summary>Shared validation + insert for CreateAsync and AdminCreateAsync — everything except who's allowed to name trainerEmployeeId in the first place.</summary>
    private async Task<TrainingRecordDto> CreateInternalAsync(Guid trainerEmployeeId, CreateTrainingRecordRequest request, CancellationToken ct)
    {
        var systemProduct = await _db.SystemProducts.Include(s => s.Client)
            .FirstOrDefaultAsync(s => s.Id == request.SystemProductId && !s.IsDeleted, ct)
            ?? throw new InvalidOperationException("System/Product not found.");

        if (string.IsNullOrWhiteSpace(request.Description))
            throw new InvalidOperationException("A description of what was taught/conducted is required.");

        var agreementType = await _db.AgreementTypes.FirstOrDefaultAsync(t => t.Id == request.AgreementTypeId, ct)
            ?? throw new InvalidOperationException("Selected training item was not found.");

        if (request.StartDateTime is { } start && request.EndDateTime is { } end && end < start)
            throw new InvalidOperationException("End date/time cannot be before the start date/time.");

        var trainer = await _db.Employees.FirstOrDefaultAsync(e => e.Id == trainerEmployeeId, ct)
            ?? throw new InvalidOperationException("Trainer not found.");

        // One training-item can only be logged once per system/product per
        // day — if Trainer A already logged "Attendance" for this product
        // today, Trainer B (or anyone else) trying to log the same item on
        // the same date is blocked, regardless of who they are. Different
        // dates, or a different training item, are unaffected — this only
        // catches the exact same session being reported twice.
        var existing = await _db.TrainingRecords
            .Include(r => r.TrainerEmployee)
            .FirstOrDefaultAsync(r =>
                r.SystemProductId == request.SystemProductId &&
                r.AgreementTypeId == request.AgreementTypeId &&
                r.TrainingDate == request.TrainingDate, ct);
        if (existing is not null)
            throw new InvalidOperationException(
                $"{agreementType.Name} for {request.TrainingDate:yyyy-MM-dd} was already logged by {existing.TrainerEmployee.FullName}.");

        var record = new TrainingRecord
        {
            SystemProductId = request.SystemProductId,
            AgreementTypeId = request.AgreementTypeId,
            TrainerEmployeeId = trainerEmployeeId,
            TrainingDate = request.TrainingDate,
            StartDateTime = request.StartDateTime,
            EndDateTime = request.EndDateTime,
            Description = request.Description.Trim(),
        };
        _db.Add(record);
        await _db.SaveChangesAsync(ct);

        return ToDto(record, systemProduct, trainer, agreementType);
    }

    /// <summary>The system/products Admin assigned this Trainer to train on — the Trainer never picks a client themselves.</summary>
    public async Task<IReadOnlyList<MyTrainingAssignmentDto>> GetAssignmentsForTrainerAsync(Guid trainerEmployeeId, CancellationToken ct = default)
    {
        return await _db.TrainingAssignments
            .Where(a => a.TrainerEmployeeId == trainerEmployeeId && !a.SystemProduct.IsDeleted && !a.SystemProduct.Client.IsDeleted)
            .OrderBy(a => a.SystemProduct.Client.Name).ThenBy(a => a.SystemProduct.Name)
            .Select(a => new MyTrainingAssignmentDto(
                a.SystemProductId, a.SystemProduct.Name, a.SystemProduct.ClientId, a.SystemProduct.Client.Name))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TrainingRecordDto>> GetForTrainerAsync(Guid trainerEmployeeId, CancellationToken ct = default)
    {
        var records = await Query().Where(r => r.TrainerEmployeeId == trainerEmployeeId)
            .OrderByDescending(r => r.TrainingDate).ThenByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
        return records.Select(r => ToDto(r, r.SystemProduct, r.TrainerEmployee, r.AgreementType)).ToList();
    }

    public async Task<IReadOnlyList<TrainingRecordDto>> GetForSystemProductAsync(Guid systemProductId, CancellationToken ct = default)
    {
        var records = await Query().Where(r => r.SystemProductId == systemProductId)
            .OrderByDescending(r => r.TrainingDate).ThenByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
        return records.Select(r => ToDto(r, r.SystemProduct, r.TrainerEmployee, r.AgreementType)).ToList();
    }

    /// <summary>
    /// Attaches/replaces a training record's supporting file. Allowed for
    /// the Trainer who logged the record, OR any Admin (e.g. reconciling
    /// scanned attendance sheets from a bulk CSV import's paper records,
    /// where the file wasn't captured by the Trainer at the time) — see
    /// TrainingRecordsController.UploadFile for how callerIsAdmin is
    /// resolved from the caller's role claim.
    /// </summary>
    public async Task<TrainingRecordDto> UploadFileAsync(Guid recordId, Guid callerEmployeeId, bool callerIsAdmin, Stream content, string fileName, string contentType, CancellationToken ct = default)
    {
        var record = await _db.TrainingRecords.Include(r => r.SystemProduct).ThenInclude(s => s.Client)
            .Include(r => r.TrainerEmployee)
            .Include(r => r.AgreementType)
            .FirstOrDefaultAsync(r => r.Id == recordId, ct)
            ?? throw new InvalidOperationException("Training record not found.");

        if (record.TrainerEmployeeId != callerEmployeeId && !callerIsAdmin)
            throw new InvalidOperationException("You can only attach a file to your own training record.");

        var previousStorageKey = record.FileStorageKey;

        var result = await _storage.SaveAsync(content, fileName, contentType, ct);

        record.FileStorageKey = result.StorageKey;
        record.FileName = result.OriginalFileName;
        _db.Update(record);
        await _db.SaveChangesAsync(ct);

        if (!string.IsNullOrEmpty(previousStorageKey))
            await _storage.DeleteAsync(previousStorageKey, ct);

        return ToDto(record, record.SystemProduct, record.TrainerEmployee, record.AgreementType);
    }

    public async Task<RetrievedFile?> DownloadFileAsync(Guid recordId, CancellationToken ct = default)
    {
        var record = await _db.TrainingRecords.AsNoTracking().FirstOrDefaultAsync(r => r.Id == recordId, ct);
        if (record is null || string.IsNullOrEmpty(record.FileStorageKey))
            return null;

        return await _storage.GetAsync(record.FileStorageKey, ct);
    }

    private IQueryable<TrainingRecord> Query() =>
        _db.TrainingRecords.AsNoTracking()
            .Include(r => r.SystemProduct).ThenInclude(s => s.Client)
            .Include(r => r.TrainerEmployee)
            .Include(r => r.AgreementType);

    private static TrainingRecordDto ToDto(TrainingRecord r, SystemProduct systemProduct, Employee trainer, AgreementType agreementType) => new(
        r.Id, r.SystemProductId, systemProduct.Name, systemProduct.ClientId, systemProduct.Client.Name,
        r.TrainerEmployeeId, trainer.FullName,
        r.AgreementTypeId, agreementType.Name,
        r.TrainingDate, r.StartDateTime, r.EndDateTime, r.Description, r.FileName, r.CreatedAt
    );
}
