using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Re-seals audit log hashes in canonical order. Run only after database backup; document the operation for compliance.
/// </summary>
public sealed class AuditChainRepairService : IAuditChainRepairService
{
    private readonly ITenantDbContextFactory _contextFactory;

    public AuditChainRepairService(ITenantDbContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Result<int>> ResealChainAsync(CancellationToken cancellationToken = default)
    {
        await using var strategyContext = _contextFactory.CreateIsolatedContext();
        var relational = strategyContext.Database.IsRelational();

        if (relational)
        {
            var strategy = strategyContext.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var ctx = _contextFactory.CreateIsolatedContext();
                await using var tx = await ctx.Database.BeginTransactionAsync(
                    System.Data.IsolationLevel.Serializable,
                    cancellationToken);
                try
                {
                    var count = await ResealCoreAsync(ctx, cancellationToken);
                    await tx.CommitAsync(cancellationToken);
                    return Result.Success(count);
                }
                catch
                {
                    await tx.RollbackAsync(cancellationToken);
                    throw;
                }
            });
        }

        await using var ctx = _contextFactory.CreateIsolatedContext();
        return Result.Success(await ResealCoreAsync(ctx, cancellationToken));
    }

    private static async Task<int> ResealCoreAsync(
        TenantDbContext ctx,
        CancellationToken cancellationToken)
    {
        var logs = await ctx.AuditLogs
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        if (logs.Count == 0)
            return 0;

        var prev = "GENESIS";
        foreach (var log in logs)
        {
            log.ResealForChainRepair(prev);
            prev = log.Hash;
        }

        await ctx.SaveChangesAsync(cancellationToken);
        return logs.Count;
    }
}
