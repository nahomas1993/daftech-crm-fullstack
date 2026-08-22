using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DaftechCrm.Application.Services;

public class FailureTypeService : IFailureTypeService
{
    private readonly IAppDbContext _db;
    public FailureTypeService(IAppDbContext db) => _db = db;

    private static FailureTypeDto ToDto(FailureType f) => new(f.Id, f.Name, f.Description, f.DurationValue, f.DurationUnit);

    public async Task<IReadOnlyList<FailureTypeDto>> GetAllAsync(CancellationToken ct = default) =>
        await _db.FailureTypes.AsNoTracking().OrderBy(x => x.Name).Select(f => new FailureTypeDto(f.Id, f.Name, f.Description, f.DurationValue, f.DurationUnit)).ToListAsync(ct);

    public async Task<FailureTypeDto> CreateAsync(CreateFailureTypeRequest request, CancellationToken ct = default)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Name is required.");
        if (request.DurationValue <= 0)
            throw new InvalidOperationException("Expected duration must be greater than zero.");

        var exists = await _db.FailureTypes.AnyAsync(x => x.Name.ToLower() == name.ToLower(), ct);
        if (exists)
            throw new InvalidOperationException($"A failure type named \"{name}\" already exists.");

        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        var entry = new FailureType { Name = name, Description = description, DurationValue = request.DurationValue, DurationUnit = request.DurationUnit };
        _db.Add(entry);
        await _db.SaveChangesAsync(ct);
        return ToDto(entry);
    }

    public async Task<FailureTypeDto> UpdateAsync(Guid id, UpdateFailureTypeRequest request, CancellationToken ct = default)
    {
        var entry = await _db.FailureTypes.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Failure type not found.");

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Name is required.");
        if (request.DurationValue <= 0)
            throw new InvalidOperationException("Expected duration must be greater than zero.");

        var exists = await _db.FailureTypes.AnyAsync(x => x.Id != id && x.Name.ToLower() == name.ToLower(), ct);
        if (exists)
            throw new InvalidOperationException($"A failure type named \"{name}\" already exists.");

        entry.Name = name;
        entry.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entry.DurationValue = request.DurationValue;
        entry.DurationUnit = request.DurationUnit;
        _db.Update(entry);
        await _db.SaveChangesAsync(ct);
        return ToDto(entry);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entry = await _db.FailureTypes.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Failure type not found.");

        _db.Remove(entry);
        await _db.SaveChangesAsync(ct);
    }
}
