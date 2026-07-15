using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

/// <summary>
/// Repository interface for company/seller entities.
/// </summary>
public interface ICompanyRepository
{
    /// <summary>
    /// Gets a company by ID.
    /// </summary>
    Task<Company?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all companies for the current tenant.
    /// </summary>
    Task<IReadOnlyList<Company>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the default (primary) company for the tenant.
    /// </summary>
    Task<Company?> GetDefaultAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a company exists.
    /// </summary>
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new company.
    /// </summary>
    Task<Company> AddAsync(Company entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing company.
    /// </summary>
    Task UpdateAsync(Company entity, CancellationToken cancellationToken = default);
}
