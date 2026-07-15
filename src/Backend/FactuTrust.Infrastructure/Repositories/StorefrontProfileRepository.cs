using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities.Storefront;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class StorefrontProfileRepository : IStorefrontProfileRepository
{
    private readonly MasterDbContext _db;

    public StorefrontProfileRepository(MasterDbContext db)
    {
        _db = db;
    }

    public Task<StorefrontProfile?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.StorefrontProfiles.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public Task<StorefrontProfile?> GetByTenantIdAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        _db.StorefrontProfiles.FirstOrDefaultAsync(p => p.TenantId == tenantId, cancellationToken);

    public Task<StorefrontProfile?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var normalized = slug.Trim().ToLowerInvariant();
        return _db.StorefrontProfiles.FirstOrDefaultAsync(p => p.Slug == normalized, cancellationToken);
    }

    public Task<bool> SlugExistsAsync(string slug, Guid? excludingStorefrontId = null, CancellationToken cancellationToken = default)
    {
        var normalized = slug.Trim().ToLowerInvariant();
        var query = _db.StorefrontProfiles.AsQueryable().Where(p => p.Slug == normalized);
        if (excludingStorefrontId is not null)
            query = query.Where(p => p.Id != excludingStorefrontId.Value);
        return query.AnyAsync(cancellationToken);
    }

    public async Task AddOptInAsync(
        StorefrontProfile profile,
        StorefrontPublishingConsent initialConsent,
        CancellationToken cancellationToken = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        _db.StorefrontProfiles.Add(profile);
        _db.StorefrontPublishingConsents.Add(initialConsent);
        await _db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }

    public async Task UpdateAsync(StorefrontProfile profile, CancellationToken cancellationToken = default)
    {
        _db.StorefrontProfiles.Update(profile);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public Task<int> CountPublishedAsync(CancellationToken cancellationToken = default) =>
        _db.StorefrontProfiles.CountAsync(p => p.Status == StorefrontStatus.Published, cancellationToken);

    public async Task<IReadOnlyList<StorefrontProfile>> ListByStatusAsync(
        StorefrontStatus status,
        CancellationToken cancellationToken = default)
    {
        return await _db.StorefrontProfiles
            .Where(p => p.Status == status)
            .OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetNextStreetPositionIndexAsync(CancellationToken cancellationToken = default)
    {
        var hasAny = await _db.StorefrontProfiles
            .AnyAsync(p => p.Status == StorefrontStatus.Published && p.StreetPositionIndex != null, cancellationToken);
        if (!hasAny)
            return 0;

        var max = await _db.StorefrontProfiles
            .Where(p => p.Status == StorefrontStatus.Published && p.StreetPositionIndex != null)
            .MaxAsync(p => p.StreetPositionIndex!.Value, cancellationToken);

        return max + 1;
    }
}
