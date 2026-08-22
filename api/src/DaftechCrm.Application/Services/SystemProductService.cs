using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DaftechCrm.Application.Services;

public class SystemProductService : ISystemProductService
{
    private readonly IAppDbContext _db;
    private readonly ReferenceNumberService _referenceNumbers;

    public SystemProductService(IAppDbContext db, ReferenceNumberService referenceNumbers)
    {
        _db = db;
        _referenceNumbers = referenceNumbers;
    }

    private static SystemProductDto ToDto(SystemProduct s) =>
        new(s.Id, s.ClientId, s.ReferenceNumber, s.Name, s.Description, s.DeploymentDate);

    /// <summary>Always inserts a new SystemProduct — never overwrites or replaces one a client already has, regardless of how many the client already has.</summary>
    public async Task<SystemProductDto> CreateAsync(CreateSystemProductRequest request, CancellationToken ct = default)
    {
        var clientExists = await _db.Clients.AnyAsync(c => c.Id == request.ClientId, ct);
        if (!clientExists)
            throw new InvalidOperationException("Client not found.");

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Name is required.");

        var entry = new SystemProduct
        {
            ClientId = request.ClientId,
            ReferenceNumber = await _referenceNumbers.GenerateSystemProductRefAsync(ct),
            Name = name,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            DeploymentDate = request.DeploymentDate,
        };
        _db.Add(entry);
        await _db.SaveChangesAsync(ct);
        return ToDto(entry);
    }

    public async Task<IReadOnlyList<SystemProductDto>> GetForClientAsync(Guid clientId, CancellationToken ct = default) =>
        await _db.SystemProducts.AsNoTracking()
            .Where(s => s.ClientId == clientId && !s.IsDeleted)
            .OrderBy(s => s.Name)
            .Select(s => new SystemProductDto(s.Id, s.ClientId, s.ReferenceNumber, s.Name, s.Description, s.DeploymentDate))
            .ToListAsync(ct);

    public async Task<SystemProductDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entry = await _db.SystemProducts.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, ct);
        return entry is null ? null : ToDto(entry);
    }

    public async Task<SystemProductDto> UpdateAsync(Guid id, UpdateSystemProductRequest request, CancellationToken ct = default)
    {
        var entry = await _db.SystemProducts.FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, ct)
            ?? throw new InvalidOperationException("System/Product not found.");

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Name is required.");

        entry.Name = name;
        entry.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entry.DeploymentDate = request.DeploymentDate;
        _db.Update(entry);
        await _db.SaveChangesAsync(ct);
        return ToDto(entry);
    }

    /// <summary>Soft-delete only — a hard delete would either orphan or FK-block its agreements. Agreement/training history stays intact and reachable through the client even after this.</summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entry = await _db.SystemProducts.FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, ct)
            ?? throw new InvalidOperationException("System/Product not found.");

        entry.IsDeleted = true;
        entry.DeletedAt = DateTimeOffset.UtcNow;
        _db.Update(entry);
        await _db.SaveChangesAsync(ct);
    }
}
