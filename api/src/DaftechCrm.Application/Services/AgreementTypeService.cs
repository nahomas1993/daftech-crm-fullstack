using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DaftechCrm.Application.Services;

public class AgreementTypeService : IAgreementTypeService
{
    private readonly IAppDbContext _db;
    public AgreementTypeService(IAppDbContext db) => _db = db;

    private static AgreementTypeDto ToDto(AgreementType t) => new(t.Id, t.Name, t.Description, t.IsSystemDefined, t.IsTrainingItem, t.IsRequiredForCompletion);

    public async Task<IReadOnlyList<AgreementTypeDto>> GetAllAsync(CancellationToken ct = default) =>
        await _db.AgreementTypes.AsNoTracking().OrderBy(t => t.Name)
            .Select(t => new AgreementTypeDto(t.Id, t.Name, t.Description, t.IsSystemDefined, t.IsTrainingItem, t.IsRequiredForCompletion)).ToListAsync(ct);

    /// <summary>Support and Training already exist from seed (see AgreementTypeNames) — this is for adding further custom types beyond those two.</summary>
    public async Task<AgreementTypeDto> CreateAsync(CreateAgreementTypeRequest request, CancellationToken ct = default)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Name is required.");

        var exists = await _db.AgreementTypes.AnyAsync(t => t.Name.ToLower() == name.ToLower(), ct);
        if (exists)
            throw new InvalidOperationException($"An agreement type named \"{name}\" already exists.");

        var entry = new AgreementType
        {
            Name = name,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            IsSystemDefined = false,
            IsTrainingItem = request.IsTrainingItem,
            IsRequiredForCompletion = request.IsTrainingItem && request.IsRequiredForCompletion,
        };
        _db.Add(entry);
        await _db.SaveChangesAsync(ct);
        return ToDto(entry);
    }

    /// <summary>
    /// Description and the two flags are editable — Name is not, since
    /// AgreementService and the training-before-support gate resolve
    /// Support/Training by name; renaming could silently break that gate
    /// for existing types. IsRequiredForCompletion is forced false whenever
    /// IsTrainingItem is false — a flag that gates SubmitTrainingAsync/
    /// MarkTrainingCompletedAsync only makes sense on a training checklist
    /// item.
    /// </summary>
    public async Task<AgreementTypeDto> UpdateAsync(Guid id, UpdateAgreementTypeRequest request, CancellationToken ct = default)
    {
        var entry = await _db.AgreementTypes.FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new InvalidOperationException("Agreement type not found.");

        entry.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entry.IsTrainingItem = request.IsTrainingItem;
        entry.IsRequiredForCompletion = request.IsTrainingItem && request.IsRequiredForCompletion;
        _db.Update(entry);
        await _db.SaveChangesAsync(ct);
        return ToDto(entry);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entry = await _db.AgreementTypes.FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new InvalidOperationException("Agreement type not found.");

        if (entry.IsSystemDefined)
            throw new InvalidOperationException($"\"{entry.Name}\" is a built-in agreement type and cannot be deleted.");

        var inUse = await _db.Agreements.AnyAsync(a => a.AgreementTypeId == id, ct);
        if (inUse)
            throw new InvalidOperationException($"\"{entry.Name}\" is still used by one or more agreements and cannot be deleted.");

        _db.Remove(entry);
        await _db.SaveChangesAsync(ct);
    }
}
