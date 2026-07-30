using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Entities.Pricing;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

/// <summary>
/// Repository du prix négocié par couple client / produit (<see cref="ClientProductPrice"/>).
/// </summary>
public interface IClientProductPriceRepository : IRepository<ClientProductPrice>
{
    /// <summary>
    /// Prix négocié pour ce client et ce produit, ou <c>null</c> s'il n'en existe aucun.
    /// </summary>
    Task<ClientProductPrice?> GetForClientProductAsync(
        Guid clientId, Guid productId, CancellationToken cancellationToken = default);

    /// <summary>Tous les prix négociés d'un client.</summary>
    Task<IReadOnlyList<ClientProductPrice>> GetByClientAsync(
        Guid clientId, CancellationToken cancellationToken = default);
}
