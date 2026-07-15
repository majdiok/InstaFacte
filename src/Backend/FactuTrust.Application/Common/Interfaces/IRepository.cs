using FactuTrust.Domain.Common;

namespace FactuTrust.Application.Common.Interfaces;

/// <summary>
/// Generic repository interface for basic CRUD operations.
/// </summary>
public interface IRepository<T> where T : Entity
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<T> AddAsync(T entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(T entity, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Unit of work interface for transaction management.
/// </summary>
/// <remarks>
/// The tenant <c>TenantDbContext</c> is configured with SQL Server retry (<c>EnableRetryOnFailure</c>).
/// Manual transactions (<see cref="BeginTransactionAsync"/>) conflict with the retry execution strategy unless the entire
/// transactional block is wrapped in <c>Database.CreateExecutionStrategy().ExecuteAsync(...)</c> with the transaction opened
/// inside that delegate. Prefer existing infrastructure services that already apply this pattern, or wrap new transactional code accordingly.
/// </remarks>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Begins a transaction on the unit-of-work database context.</summary>
    /// <remarks>See interface remarks: do not use with retry-enabled tenant DB unless wrapped in an execution strategy.</remarks>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>Commits the current transaction.</summary>
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>Rolls back the current transaction.</summary>
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
