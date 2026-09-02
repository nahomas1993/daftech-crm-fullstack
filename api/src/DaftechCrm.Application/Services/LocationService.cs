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

        static LocationEntryDto ToDto(LocationEntry e) => new(e.Id, e.Type, e.Name, e.ParentId);

        return new LocationOptionsDto(
            all.Where(x => x.Type == LocationType.Region).Select(ToDto).ToList(),
            all.Where(x => x.Type == LocationType.Zone).Select(ToDto).ToList(),
            all.Where(x => x.Type == LocationType.City).Select(ToDto).ToList(),
            all.Where(x => x.Type == LocationType.Woreda).Select(ToDto).ToList(),
            all.Where(x => x.Type == LocationType.Specialization).Select(ToDto).ToList(),
            all.Where(x => x.Type == LocationType.CustomRole).Select(ToDto).ToList()
        );
    }

    /// <summary>Zone requires a ParentId pointing at an existing Region; Woreda requires a ParentId pointing at an existing Zone. Every other type must not carry a ParentId.</summary>
    private static LocationType? RequiredParentType(LocationType type) => type switch
    {
        LocationType.Zone => LocationType.Region,
        LocationType.Woreda => LocationType.Zone,
        _ => null,
    };

    public async Task<LocationEntryDto> CreateAsync(CreateLocationEntryRequest request, CancellationToken ct = default)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Name is required.");

        var requiredParentType = RequiredParentType(request.Type);
        if (requiredParentType is not null)
        {
            if (request.ParentId is null)
                throw new InvalidOperationException($"A {requiredParentType} must be selected for a {request.Type}.");

            var parent = await _db.LocationEntries.FirstOrDefaultAsync(x => x.Id == request.ParentId, ct)
                ?? throw new InvalidOperationException("Selected parent location was not found.");
            if (parent.Type != requiredParentType)
                throw new InvalidOperationException($"The selected parent must be a {requiredParentType}.");
        }
        else if (request.ParentId is not null)
        {
            throw new InvalidOperationException($"{request.Type} entries cannot have a parent.");
        }

        var exists = await _db.LocationEntries.AnyAsync(x => x.Type == request.Type && x.Name.ToLower() == name.ToLower(), ct);
        if (exists)
            throw new InvalidOperationException($"\"{name}\" already exists in {request.Type}.");

        var entry = new LocationEntry { Type = request.Type, Name = name, ParentId = requiredParentType is null ? null : request.ParentId };
        _db.Add(entry);
        await _db.SaveChangesAsync(ct);
        return new LocationEntryDto(entry.Id, entry.Type, entry.Name, entry.ParentId);
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
        return new LocationEntryDto(entry.Id, entry.Type, entry.Name, entry.ParentId);
    }

    /// <summary>Deleting a Region or Zone cascades to its descendants (Zones/Woredas beneath it) at the database level — see LocationEntryConfiguration's self-referencing Cascade delete.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entry = await _db.LocationEntries.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("Location entry not found.");

        _db.Remove(entry); // NOTE: requires the new IAppDbContext.Remove<TEntity>() method — see AppDbContext.cs additions.
        await _db.SaveChangesAsync(ct);
    }
}
