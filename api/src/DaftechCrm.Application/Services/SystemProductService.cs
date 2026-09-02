using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Domain.Entities;
using DaftechCrm.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DaftechCrm.Application.Services;

public class SystemProductService : ISystemProductService
{
    /// <summary>Settings registry key for the ceiling on how many Trainers/Technicians can be assigned to one system/product's training roster — see SystemConfigurationService.</summary>
    public const string MaxTrainersSettingKey = "Training.MaxTrainersPerSystemProduct";

    private readonly IAppDbContext _db;
    private readonly ReferenceNumberService _referenceNumbers;
    private readonly ITrainerWorkloadService _trainerWorkload;
    private readonly ISystemConfigurationService _config;

    public SystemProductService(
        IAppDbContext db, ReferenceNumberService referenceNumbers,
        ITrainerWorkloadService trainerWorkload, ISystemConfigurationService config)
    {
        _db = db;
        _referenceNumbers = referenceNumbers;
        _trainerWorkload = trainerWorkload;
        _config = config;
    }

    private static SystemProductDto ToDto(SystemProduct s) => new(
        s.Id, s.ClientId, s.ReferenceNumber, s.Name, s.Description, s.DeploymentDate,
        s.TrainingCompletionStatus,
        s.TrainingAssignments.OrderBy(a => a.AssignedAt)
            .Select(a => new TrainingAssignmentDto(a.Id, a.TrainerEmployeeId, a.TrainerEmployee?.FullName ?? "(unknown)", a.AssignedAt))
            .ToList(),
        s.CatalogItemId, s.ExpiryDate, s.TrainingSubmittedAt
    );

    /// <summary>Always inserts a new SystemProduct — never overwrites or replaces one a client already has, regardless of how many the client already has. Starts with an empty training roster and TrainingCompletionStatus.NotStarted.</summary>
    public async Task<SystemProductDto> CreateAsync(CreateSystemProductRequest request, CancellationToken ct = default)
    {
        var clientExists = await _db.Clients.AnyAsync(c => c.Id == request.ClientId, ct);
        if (!clientExists)
            throw new InvalidOperationException("Client not found.");

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Name is required.");

        if (request.CatalogItemId is Guid catalogId)
        {
            var catalogExists = await _db.ProductCatalogItems.AnyAsync(c => c.Id == catalogId, ct);
            if (!catalogExists)
                throw new InvalidOperationException("Selected system/product catalog entry was not found.");
        }

        var entry = new SystemProduct
        {
            ClientId = request.ClientId,
            ReferenceNumber = await _referenceNumbers.GenerateSystemProductRefAsync(ct),
            Name = name,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            DeploymentDate = request.DeploymentDate,
            CatalogItemId = request.CatalogItemId,
            ExpiryDate = request.ExpiryDate,
        };
        _db.Add(entry);
        await _db.SaveChangesAsync(ct);
        return ToDto(entry);
    }

    public async Task<IReadOnlyList<SystemProductDto>> GetForClientAsync(Guid clientId, CancellationToken ct = default)
    {
        var entries = await Query().Where(s => s.ClientId == clientId && !s.IsDeleted).OrderBy(s => s.Name).ToListAsync(ct);
        return entries.Select(ToDto).ToList();
    }

    public async Task<SystemProductDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entry = await Query().FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, ct);
        return entry is null ? null : ToDto(entry);
    }

    public async Task<SystemProductDto> UpdateAsync(Guid id, UpdateSystemProductRequest request, CancellationToken ct = default)
    {
        var entry = await _db.SystemProducts.Include(s => s.TrainingAssignments).ThenInclude(a => a.TrainerEmployee)
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, ct)
            ?? throw new InvalidOperationException("System/Product not found.");

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Name is required.");

        if (request.CatalogItemId is Guid catalogId)
        {
            var catalogExists = await _db.ProductCatalogItems.AnyAsync(c => c.Id == catalogId, ct);
            if (!catalogExists)
                throw new InvalidOperationException("Selected system/product catalog entry was not found.");
        }

        entry.Name = name;
        entry.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entry.DeploymentDate = request.DeploymentDate;
        entry.CatalogItemId = request.CatalogItemId;
        entry.ExpiryDate = request.ExpiryDate;
        _db.Update(entry);
        await _db.SaveChangesAsync(ct);
        return ToDto(entry);
    }

    /// <summary>Soft-delete only — a hard delete would either orphan or FK-block its agreements/training records. Agreement/training history stays intact and reachable through the client even after this.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entry = await _db.SystemProducts.FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, ct)
            ?? throw new InvalidOperationException("System/Product not found.");

        entry.IsDeleted = true;
        entry.DeletedAt = DateTimeOffset.UtcNow;
        _db.Update(entry);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> HasCompletedTrainingAsync(Guid systemProductId, CancellationToken ct = default) =>
        await _db.SystemProducts.AsNoTracking()
            .Where(s => s.Id == systemProductId)
            .Select(s => s.TrainingCompletionStatus)
            .FirstOrDefaultAsync(ct) == TrainingCompletionStatus.Completed;

    public async Task<SystemProductDto> AddTrainingAssignmentAsync(Guid systemProductId, Guid trainerEmployeeId, CancellationToken ct = default)
    {
        var entry = await _db.SystemProducts.Include(s => s.TrainingAssignments).ThenInclude(a => a.TrainerEmployee)
            .FirstOrDefaultAsync(s => s.Id == systemProductId && !s.IsDeleted, ct)
            ?? throw new InvalidOperationException("System/Product not found.");

        if (entry.TrainingAssignments.Any(a => a.TrainerEmployeeId == trainerEmployeeId))
            throw new InvalidOperationException("This employee is already on this system/product's training roster.");

        var maxTrainers = await _config.GetIntAsync(MaxTrainersSettingKey, ct);
        if (entry.TrainingAssignments.Count >= maxTrainers)
            throw new InvalidOperationException($"This system/product's training roster is already at the configured maximum of {maxTrainers} trainer(s).");

        var trainer = await _db.Employees.FirstOrDefaultAsync(e => e.Id == trainerEmployeeId && !e.IsDeleted, ct)
            ?? throw new InvalidOperationException("Trainer not found.");
        if (!trainer.Roles.Contains(EmployeeRole.Trainer))
            throw new InvalidOperationException("This employee does not have the Trainer responsibility assigned.");

        _db.Add(new TrainingAssignment { SystemProductId = systemProductId, TrainerEmployeeId = trainerEmployeeId });
        await _db.SaveChangesAsync(ct);

        return (await GetByIdAsync(systemProductId, ct))!;
    }

    /// <summary>Automatic Assignment — fills empty roster slots (up to the configured maximum) by workload, skipping any employee already on the roster. A no-op if the roster is already full.</summary>
    public async Task<SystemProductDto> AutoAssignTrainersAsync(Guid systemProductId, CancellationToken ct = default)
    {
        var entry = await _db.SystemProducts.Include(s => s.TrainingAssignments).ThenInclude(a => a.TrainerEmployee)
            .FirstOrDefaultAsync(s => s.Id == systemProductId && !s.IsDeleted, ct)
            ?? throw new InvalidOperationException("System/Product not found.");

        var maxTrainers = await _config.GetIntAsync(MaxTrainersSettingKey, ct);
        var openSlots = maxTrainers - entry.TrainingAssignments.Count;
        if (openSlots <= 0)
            return ToDto(entry);

        var alreadyAssignedIds = entry.TrainingAssignments.Select(a => a.TrainerEmployeeId).ToHashSet();

        // Ask for more than we need, in case some of the top-ranked
        // candidates are already on this roster — SelectTrainersForAssignmentAsync
        // has no way to know that itself, since it ranks across all
        // system/products, not just this one.
        var candidates = await _trainerWorkload.SelectTrainersForAssignmentAsync(openSlots + alreadyAssignedIds.Count, ct);
        var toAssign = candidates.Where(id => !alreadyAssignedIds.Contains(id)).Take(openSlots).ToList();

        foreach (var trainerId in toAssign)
            _db.Add(new TrainingAssignment { SystemProductId = systemProductId, TrainerEmployeeId = trainerId });

        if (toAssign.Count > 0)
            await _db.SaveChangesAsync(ct);

        return (await GetByIdAsync(systemProductId, ct))!;
    }

    public async Task<SystemProductDto> RemoveTrainingAssignmentAsync(Guid systemProductId, Guid assignmentId, CancellationToken ct = default)
    {
        var assignment = await _db.TrainingAssignments.FirstOrDefaultAsync(a => a.Id == assignmentId && a.SystemProductId == systemProductId, ct)
            ?? throw new InvalidOperationException("Training assignment not found.");

        _db.Remove(assignment);
        await _db.SaveChangesAsync(ct);

        return (await GetByIdAsync(systemProductId, ct))!;
    }

    public async Task<SystemProductDto> MarkTrainingCompletedAsync(Guid systemProductId, CancellationToken ct = default)
    {
        var entry = await _db.SystemProducts.FirstOrDefaultAsync(s => s.Id == systemProductId && !s.IsDeleted, ct)
            ?? throw new InvalidOperationException("System/Product not found.");

        await EnsureRequiredTrainingItemsCoveredAsync(systemProductId, ct);

        entry.TrainingCompletionStatus = TrainingCompletionStatus.Completed;
        _db.Update(entry);
        await _db.SaveChangesAsync(ct);

        return (await GetByIdAsync(systemProductId, ct))!;
    }

    /// <summary>Trainer's own "done, submit to Admin" action — see ISystemProductService.SubmitTrainingAsync.</summary>
    public async Task<SystemProductDto> SubmitTrainingAsync(Guid systemProductId, Guid callerEmployeeId, CancellationToken ct = default)
    {
        var entry = await _db.SystemProducts.FirstOrDefaultAsync(s => s.Id == systemProductId && !s.IsDeleted, ct)
            ?? throw new InvalidOperationException("System/Product not found.");

        var onRoster = await _db.TrainingAssignments
            .AnyAsync(a => a.SystemProductId == systemProductId && a.TrainerEmployeeId == callerEmployeeId, ct);
        if (!onRoster)
            throw new InvalidOperationException("You are not assigned to train on this client's system/product.");

        var hasAnyRecord = await _db.TrainingRecords.AnyAsync(r => r.SystemProductId == systemProductId, ct);
        if (!hasAnyRecord)
            throw new InvalidOperationException("Log at least one training item before submitting.");

        await EnsureRequiredTrainingItemsCoveredAsync(systemProductId, ct);

        entry.TrainingSubmittedAt = DateTimeOffset.UtcNow;
        if (entry.TrainingCompletionStatus == TrainingCompletionStatus.NotStarted)
            entry.TrainingCompletionStatus = TrainingCompletionStatus.InProgress;
        _db.Update(entry);
        await _db.SaveChangesAsync(ct);

        return (await GetByIdAsync(systemProductId, ct))!;
    }

    /// <summary>
    /// Every admin-configured AgreementType with IsRequiredForCompletion
    /// must have at least one matching TrainingRecord logged against this
    /// SystemProduct before training can be submitted or marked Completed.
    /// A no-op when no AgreementType is currently flagged required — the
    /// gate only ever gets stricter by an Admin explicitly opting an item
    /// in via AgreementTypeService.
    /// </summary>
    private async Task EnsureRequiredTrainingItemsCoveredAsync(Guid systemProductId, CancellationToken ct)
    {
        var requiredTypeIds = await _db.AgreementTypes
            .AsNoTracking()
            .Where(t => t.IsRequiredForCompletion)
            .Select(t => t.Id)
            .ToListAsync(ct);

        if (requiredTypeIds.Count == 0)
            return;

        var coveredTypeIds = await _db.TrainingRecords
            .AsNoTracking()
            .Where(r => r.SystemProductId == systemProductId && requiredTypeIds.Contains(r.AgreementTypeId))
            .Select(r => r.AgreementTypeId)
            .Distinct()
            .ToListAsync(ct);

        var missingTypeIds = requiredTypeIds.Except(coveredTypeIds).ToList();
        if (missingTypeIds.Count == 0)
            return;

        var missingNames = await _db.AgreementTypes
            .AsNoTracking()
            .Where(t => missingTypeIds.Contains(t.Id))
            .Select(t => t.Name)
            .ToListAsync(ct);

        throw new InvalidOperationException(
            $"The following required training item(s) have no logged session yet: {string.Join(", ", missingNames)}.");
    }

    private IQueryable<SystemProduct> Query() =>
        _db.SystemProducts.AsNoTracking().Include(s => s.TrainingAssignments).ThenInclude(a => a.TrainerEmployee);
}
