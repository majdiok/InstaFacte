using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Entities.Studio;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.Services.Studio;

/// <summary>
/// Atomic AutoNumber allocator. Reuses the proven invoice-numbering pattern: an execution strategy
/// wraps a Serializable transaction that get-or-creates the per-field counter, increments it, and
/// commits — so concurrent record creations never collide on a value. One physical DB per tenant.
/// </summary>
public sealed class CustomSequenceAllocator : ICustomSequenceAllocator
{
    private readonly IDbContextFactory<TenantDbContext> _contextFactory;
    private readonly ILogger<CustomSequenceAllocator> _logger;

    public CustomSequenceAllocator(
        IDbContextFactory<TenantDbContext> contextFactory,
        ILogger<CustomSequenceAllocator> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<long> ReserveNextAsync(
        Guid tenantId, Guid entityDefinitionId, string fieldKey, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var strategy = context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(
                System.Data.IsolationLevel.Serializable, cancellationToken);

            try
            {
                var counter = await context.CustomFieldSequences
                    .FirstOrDefaultAsync(
                        s => s.TenantId == tenantId && s.EntityDefinitionId == entityDefinitionId && s.FieldKey == fieldKey,
                        cancellationToken);

                if (counter is null)
                {
                    counter = CustomFieldSequence.Create(tenantId, entityDefinitionId, fieldKey);
                    context.CustomFieldSequences.Add(counter);
                }

                var value = counter.ReserveNext();

                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                _logger.LogDebug(
                    "Reserved AutoNumber {Value} for tenant {TenantId}, entity {EntityId}, field {FieldKey}",
                    value, tenantId, entityDefinitionId, fieldKey);

                return value;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }
}
