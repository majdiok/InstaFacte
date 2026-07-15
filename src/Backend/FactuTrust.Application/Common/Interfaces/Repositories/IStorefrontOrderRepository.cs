using FactuTrust.Domain.Entities.Storefront;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface IStorefrontOrderRepository
{
    Task AddAsync(StorefrontOrder order, CancellationToken cancellationToken = default);
}
