using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DaftechCrm.Application.Services;

public class LocationService : ILocationService
{
    private readonly IAppDbContext _db;
    public LocationService(IAppDbContext db) => _db = db;

    public async Task<LocationOptionsDto> GetAllAsync(CancellationToken ct = default)
    {
        var all = await _db.LocationEntries.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct);

        static LocationEntryDto ToDto(LocationEntry e) => new(e.Id, e.Type, e.Name);

        return new LocationOptionsDto(
            all.Where(x => x.Type == LocationType.Region).Select(ToDto).ToList(),
            all.Where(x => x.Type == LocationType.City).Select(ToDto).ToList(),
            all.Where(x => x.Type == LocationType.Woreda).Select(ToDto).ToList(),
            all.Where(x => x.Type == LocationType.Specialization).Select(ToDto).ToList(),
            all.Where(x => x.Type == LocationType.CustomRole).Select(ToDto).ToList()
        );
    }

    public async Task<LocationEntryDto> CreateAsync(CreateLocationEntryRequest request, CancellationToken ct = default)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Name is required.");

        var exists = await _db.LocationEntries.AnyAsync(x => x.Type == request.Type && x.Name.ToLower() == name.ToLower(), ct);
        if (exists)
            throw new InvalidOperationException($"\"{name}\" already exists in {request.Type}.");

        var entry = new LocationEntry { Type = request.Type, Name = name };
        _db.Add(entry);
        await _db.SaveChangesAsync(ct);
        return new LocationEntryDto(entry.Id, entry.Type, entry.Name);
    }

    public async Task<LocationEntryDto> UpdateAsync(Guid id, UpdateLocationEntryRequest request, CancellationToken ct = default)
    {
        var entry = await _db.LocationEntries.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Location entry not found.");

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Name is required.");

        var exists = await _db.LocationEntries.AnyAsync(x => x.Type == entry.Type && x.Id != id && x.Name.ToLower() == name.ToLower(), ct);
        if (exists)
            throw new InvalidOperationException($"\"{name}\" already exists in {entry.Type}.");

        entry.Name = name;
        _db.Update(entry);
        await _db.SaveChangesAsync(ct);
        return new LocationEntryDto(entry.Id, entry.Type, entry.Name);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entry = await _db.LocationEntries.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Location entry not found.");

        _db.Remove(entry); // NOTE: requires the new IAppDbContext.Remove<TEntity>() method — see AppDbContext.cs additions.
        await _db.SaveChangesAsync(ct);
    }
}
