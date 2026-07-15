using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

/// <summary>
/// Repository for tenant tax catalog (TVA, timbres, etc.).
/// </summary>
public interface ITaxRepository
{
    Task<IReadOnlyList<Tax>> GetAllAsync(
        TaxType? type = null,
        TaxContext? context = null,
        bool? activeOnly = null,
        CancellationToken cancellationToken = default);

    Task<Tax?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Tax>> GetActiveVatRatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Active fixed stamp tax for sales (or all contexts). Used for fiscal stamp on invoices.
    /// </summary>
    Task<Tax?> GetActiveSalesStampTaxAsync(CancellationToken cancellationToken = default);

    Task<Tax> AddAsync(Tax entity, CancellationToken cancellationToken = default);

    Task UpdateAsync(Tax entity, CancellationToken cancellationToken = default);

    Task DeleteAsync(Tax entity, CancellationToken cancellationToken = default);
}
