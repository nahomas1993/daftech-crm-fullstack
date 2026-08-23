using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Domain.Entities;
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
        var systemProduct = await _db.SystemProducts.Include(s => s.Client)
            .FirstOrDefaultAsync(s => s.Id == request.SystemProductId && !s.IsDeleted, ct)
            ?? throw new InvalidOperationException("System/Product not found.");

        var onRoster = await _db.TrainingAssignments
            .AnyAsync(a => a.SystemProductId == request.SystemProductId && a.TrainerEmployeeId == callerEmployeeId, ct);
        if (!onRoster)
            throw new InvalidOperationException("You are not assigned to train on this client's system/product.");

        if (string.IsNullOrWhiteSpace(request.Description))
            throw new InvalidOperationException("A description of what was taught/conducted is required.");

        var trainer = await _db.Employees.FirstOrDefaultAsync(e => e.Id == callerEmployeeId, ct)
            ?? throw new InvalidOperationException("Trainer not found.");

        var record = new TrainingRecord
        {
            SystemProductId = request.SystemProductId,
            TrainerEmployeeId = callerEmployeeId,
            TrainingDate = request.TrainingDate,
            Description = request.Description.Trim(),
        };
        _db.Add(record);
        await _db.SaveChangesAsync(ct);

        return ToDto(record, systemProduct, trainer);
    }

    public async Task<IReadOnlyList<TrainingRecordDto>> GetForTrainerAsync(Guid trainerEmployeeId, CancellationToken ct = default)
    {
        var records = await Query().Where(r => r.TrainerEmployeeId == trainerEmployeeId)
            .OrderByDescending(r => r.TrainingDate).ThenByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
        return records.Select(r => ToDto(r, r.SystemProduct, r.TrainerEmployee)).ToList();
    }

    public async Task<IReadOnlyList<TrainingRecordDto>> GetForSystemProductAsync(Guid systemProductId, CancellationToken ct = default)
    {
        var records = await Query().Where(r => r.SystemProductId == systemProductId)
            .OrderByDescending(r => r.TrainingDate).ThenByDescending(r => r.CreatedAt)
            .ToListAsync(ct);
        return records.Select(r => ToDto(r, r.SystemProduct, r.TrainerEmployee)).ToList();
    }

    public async Task<TrainingRecordDto> UploadFileAsync(Guid recordId, Guid callerEmployeeId, Stream content, string fileName, string contentType, CancellationToken ct = default)
    {
        var record = await _db.TrainingRecords.Include(r => r.SystemProduct).ThenInclude(s => s.Client)
            .Include(r => r.TrainerEmployee)
            .FirstOrDefaultAsync(r => r.Id == recordId, ct)
            ?? throw new InvalidOperationException("Training record not found.");

        if (record.TrainerEmployeeId != callerEmployeeId)
            throw new InvalidOperationException("You can only attach a file to your own training record.");

        var previousStorageKey = record.FileStorageKey;

        var result = await _storage.SaveAsync(content, fileName, contentType, ct);

        record.FileStorageKey = result.StorageKey;
        record.FileName = result.OriginalFileName;
        _db.Update(record);
        await _db.SaveChangesAsync(ct);

        if (!string.IsNullOrEmpty(previousStorageKey))
            await _storage.DeleteAsync(previousStorageKey, ct);

        return ToDto(record, record.SystemProduct, record.TrainerEmployee);
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
            .Include(r => r.TrainerEmployee);

    private static TrainingRecordDto ToDto(TrainingRecord r, SystemProduct systemProduct, Employee trainer) => new(
        r.Id, r.SystemProductId, systemProduct.Name, systemProduct.ClientId, systemProduct.Client.Name,
        r.TrainerEmployeeId, trainer.FullName,
        r.TrainingDate, r.Description, r.FileName, r.CreatedAt
    );
}
