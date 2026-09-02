using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DaftechCrm.Application.Services;

public class FailureTypeService : IFailureTypeService
{
    private readonly IAppDbContext _db;
    private readonly IMemoryCache _cache;
    private const string CacheKey = "failure-types:all";
    public FailureTypeService(IAppDbContext db, IMemoryCache cache) { _db = db; _cache = cache; }

    private static FailureTypeDto ToDto(FailureType f) => new(f.Id, f.Category, f.Name, f.Description, f.BasePrice, f.DurationValue, f.DurationUnit, f.RequiredSpecialization);

    public async Task<IReadOnlyList<FailureTypeDto>> GetAllAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CacheKey, out IReadOnlyList<FailureTypeDto>? cached) && cached is not null)
            return cached;

        var result = await _db.FailureTypes.AsNoTracking().OrderBy(x => x.Category).ThenBy(x => x.Name)
            .Select(f => new FailureTypeDto(f.Id, f.Category, f.Name, f.Description, f.BasePrice, f.DurationValue, f.DurationUnit, f.RequiredSpecialization)).ToListAsync(ct);
        _cache.Set(CacheKey, result, TimeSpan.FromMinutes(10));
        return result;
    }

    public async Task<FailureTypeDto> CreateAsync(CreateFailureTypeRequest request, CancellationToken ct = default)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Name is required.");
        if (request.DurationValue <= 0)
            throw new InvalidOperationException("Expected duration must be greater than zero.");
        if (request.BasePrice < 0)
            throw new InvalidOperationException("The base price can't be negative.");

        var exists = await _db.FailureTypes.AnyAsync(x => x.Name.ToLower() == name.ToLower(), ct);
        if (exists)
            throw new InvalidOperationException($"A failure type named \"{name}\" already exists.");

        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        var specialization = string.IsNullOrWhiteSpace(request.RequiredSpecialization) ? null : request.RequiredSpecialization.Trim();
        var entry = new FailureType { Category = request.Category, Name = name, Description = description, BasePrice = request.BasePrice, DurationValue = request.DurationValue, DurationUnit = request.DurationUnit, RequiredSpecialization = specialization };
        _db.Add(entry);
        await _db.SaveChangesAsync(ct);
        _cache.Remove(CacheKey);
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
        if (request.BasePrice < 0)
            throw new InvalidOperationException("The base price can't be negative.");

        var exists = await _db.FailureTypes.AnyAsync(x => x.Id != id && x.Name.ToLower() == name.ToLower(), ct);
        if (exists)
            throw new InvalidOperationException($"A failure type named \"{name}\" already exists.");

        entry.Category = request.Category;
        entry.Name = name;
        entry.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entry.BasePrice = request.BasePrice;
        entry.DurationValue = request.DurationValue;
        entry.DurationUnit = request.DurationUnit;
        entry.RequiredSpecialization = string.IsNullOrWhiteSpace(request.RequiredSpecialization) ? null : request.RequiredSpecialization.Trim();
        _db.Update(entry);
        await _db.SaveChangesAsync(ct);
        _cache.Remove(CacheKey);
        return ToDto(entry);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entry = await _db.FailureTypes.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Failure type not found.");

        _db.Remove(entry);
        await _db.SaveChangesAsync(ct);
        _cache.Remove(CacheKey);
    }
}
