using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DaftechCrm.Application.Services;

/// <summary>
/// Admin-managed support types and their extra fees. Cached the same way
/// failure types are — the list changes rarely but is read on every visit
/// to the client portal's submit form.
/// </summary>
public class SupportTypeService : ISupportTypeService
{
    private readonly IAppDbContext _db;
    private readonly IMemoryCache _cache;
    private const string CacheKey = "support-types:all";

    public SupportTypeService(IAppDbContext db, IMemoryCache cache) { _db = db; _cache = cache; }

    private static SupportTypeDto ToDto(SupportType s) => new(s.Id, s.Name, s.Description, s.AdditionalFee);

    public async Task<IReadOnlyList<SupportTypeDto>> GetAllAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CacheKey, out IReadOnlyList<SupportTypeDto>? cached) && cached is not null)
            return cached;

        var result = await _db.SupportTypes.AsNoTracking().OrderBy(x => x.Name)
            .Select(s => new SupportTypeDto(s.Id, s.Name, s.Description, s.AdditionalFee)).ToListAsync(ct);
        _cache.Set(CacheKey, result, TimeSpan.FromMinutes(10));
        return result;
    }

    public async Task<SupportTypeDto> CreateAsync(CreateSupportTypeRequest request, CancellationToken ct = default)
    {
        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Please give this support type a name.");
        if (request.AdditionalFee < 0)
            throw new InvalidOperationException("The additional fee can't be negative.");

        var exists = await _db.SupportTypes.AnyAsync(x => x.Name.ToLower() == name.ToLower(), ct);
        if (exists)
            throw new InvalidOperationException($"There's already a support type called \"{name}\".");

        var entry = new SupportType
        {
            Name = name,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            AdditionalFee = request.AdditionalFee,
        };

        _db.Add(entry);
        await _db.SaveChangesAsync(ct);
        _cache.Remove(CacheKey);
        return ToDto(entry);
    }

    public async Task<SupportTypeDto> UpdateAsync(Guid id, UpdateSupportTypeRequest request, CancellationToken ct = default)
    {
        var entry = await _db.SupportTypes.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("We couldn't find that support type.");

        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Please give this support type a name.");
        if (request.AdditionalFee < 0)
            throw new InvalidOperationException("The additional fee can't be negative.");

        var exists = await _db.SupportTypes.AnyAsync(x => x.Id != id && x.Name.ToLower() == name.ToLower(), ct);
        if (exists)
            throw new InvalidOperationException($"There's already a support type called \"{name}\".");

        entry.Name = name;
        entry.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entry.AdditionalFee = request.AdditionalFee;
        _db.Update(entry);
        await _db.SaveChangesAsync(ct);
        _cache.Remove(CacheKey);
        return ToDto(entry);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entry = await _db.SupportTypes.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("We couldn't find that support type.");

        _db.Remove(entry);
        await _db.SaveChangesAsync(ct);
        _cache.Remove(CacheKey);
    }
}
