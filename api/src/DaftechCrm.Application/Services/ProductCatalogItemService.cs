using DaftechCrm.Application.DTOs;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DaftechCrm.Application.Services;

/// <summary>
/// Admin-managed catalog of system/product names (see ProductCatalogItem)
/// — configurable from Settings without a code change, matching the
/// FailureType/SupportType lookup pattern exactly. Cached the same way:
/// the list changes rarely but is read on every visit to client
/// registration, "Add System/Product", and the Submit Issue form.
/// </summary>
public class ProductCatalogItemService : IProductCatalogItemService
{
    private readonly IAppDbContext _db;
    private readonly IMemoryCache _cache;
    private const string CacheKey = "product-catalog-items:active";

    public ProductCatalogItemService(IAppDbContext db, IMemoryCache cache) { _db = db; _cache = cache; }

    private static ProductCatalogItemDto ToDto(ProductCatalogItem p) => new(p.Id, p.Name, p.Description, p.IsActive);

    public async Task<IReadOnlyList<ProductCatalogItemDto>> GetAllAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CacheKey, out IReadOnlyList<ProductCatalogItemDto>? cached) && cached is not null)
            return cached;

        var result = await _db.ProductCatalogItems.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new ProductCatalogItemDto(x.Id, x.Name, x.Description, x.IsActive))
            .ToListAsync(ct);
        _cache.Set(CacheKey, result, TimeSpan.FromMinutes(10));
        return result;
    }

    public async Task<IReadOnlyList<ProductCatalogItemDto>> GetAllForAdminAsync(CancellationToken ct = default) =>
        await _db.ProductCatalogItems.AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new ProductCatalogItemDto(x.Id, x.Name, x.Description, x.IsActive))
            .ToListAsync(ct);

    public async Task<ProductCatalogItemDto> CreateAsync(CreateProductCatalogItemRequest request, CancellationToken ct = default)
    {
        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Please give this system/product a name.");

        var exists = await _db.ProductCatalogItems.AnyAsync(x => x.Name.ToLower() == name.ToLower(), ct);
        if (exists)
            throw new InvalidOperationException($"There's already a system/product called \"{name}\".");

        var entry = new ProductCatalogItem
        {
            Name = name,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
        };

        _db.Add(entry);
        await _db.SaveChangesAsync(ct);
        _cache.Remove(CacheKey);
        return ToDto(entry);
    }

    public async Task<ProductCatalogItemDto> UpdateAsync(Guid id, UpdateProductCatalogItemRequest request, CancellationToken ct = default)
    {
        var entry = await _db.ProductCatalogItems.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("We couldn't find that system/product.");

        var name = (request.Name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Please give this system/product a name.");

        var exists = await _db.ProductCatalogItems.AnyAsync(x => x.Id != id && x.Name.ToLower() == name.ToLower(), ct);
        if (exists)
            throw new InvalidOperationException($"There's already a system/product called \"{name}\".");

        entry.Name = name;
        entry.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entry.IsActive = request.IsActive;
        _db.Update(entry);
        await _db.SaveChangesAsync(ct);
        _cache.Remove(CacheKey);
        return ToDto(entry);
    }

    /// <summary>
    /// Removing a catalog entry an Admin no longer wants to offer never
    /// needs to be a hard delete — clients/systems that already reference
    /// it (SystemProduct.CatalogItemId) would otherwise lose that link.
    /// Instead this simply deactivates it (IsActive = false), consistent
    /// with the "add, edit, and remove ... without changing the code"
    /// requirement while preserving history: it disappears from the
    /// active dropdown but stays resolvable by Id for anything already
    /// pointing at it.
    /// </summary>
    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entry = await _db.ProductCatalogItems.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new InvalidOperationException("We couldn't find that system/product.");

        entry.IsActive = false;
        _db.Update(entry);
        await _db.SaveChangesAsync(ct);
        _cache.Remove(CacheKey);
    }
}
