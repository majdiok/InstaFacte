using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Entities.Storefront;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Repositories;

public sealed class StorefrontOrderRepository : IStorefrontOrderRepository
{
    private readonly MasterDbContext _db;

    public StorefrontOrderRepository(MasterDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(StorefrontOrder order, CancellationToken cancellationToken = default)
    {
        _db.StorefrontOrders.Add(order);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
