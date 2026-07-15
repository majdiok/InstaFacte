using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities.Storefront;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class StorefrontPublishingConsentRepository : IStorefrontPublishingConsentRepository
{
    private readonly MasterDbContext _db;

    public StorefrontPublishingConsentRepository(MasterDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(StorefrontPublishingConsent consent, CancellationToken cancellationToken = default)
    {
        _db.StorefrontPublishingConsents.Add(consent);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StorefrontPublishingConsent>> GetByStorefrontAsync(
        Guid storefrontProfileId,
        CancellationToken cancellationToken = default)
    {
        var list = await _db.StorefrontPublishingConsents
            .Where(c => c.StorefrontProfileId == storefrontProfileId)
            .OrderByDescending(c => c.AcceptedAt)
            .ToListAsync(cancellationToken);
        return list;
    }
}
