using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.MultiTenancy;

/// <summary>
/// Implémentation de <see cref="ITenantUnitOfWork"/> : ouvre un contexte racine AVEC retry,
/// enveloppe l'action dans <c>ExecutionStrategy.ExecuteAsync</c> (même motif que
/// DocumentNumberService) et publie la connexion/transaction dans
/// <see cref="TenantAmbientTransaction"/> pour que les contextes créés par
/// <see cref="TenantDbContextFactory.CreateContext"/> s'y enrôlent.
/// En cas de retry transitoire SQL, l'action est ré-exécutée entièrement dans une
/// nouvelle transaction (les générations d'écritures/mouvements sont idempotentes).
/// </summary>
public sealed class TenantUnitOfWork : ITenantUnitOfWork
{
    private readonly ITenantDbContextFactory _contextFactory;
    private readonly TenantAmbientTransaction _ambient;
    private readonly ILogger<TenantUnitOfWork> _logger;

    public TenantUnitOfWork(
        ITenantDbContextFactory contextFactory,
        TenantAmbientTransaction ambient,
        ILogger<TenantUnitOfWork> logger)
    {
        _contextFactory = contextFactory;
        _ambient = ambient;
        _logger = logger;
    }

    public Task<Result> ExecuteAsync(Func<CancellationToken, Task<Result>> action, CancellationToken cancellationToken = default)
        => ExecuteCoreAsync(action, r => r.IsSuccess, cancellationToken);

    public Task<Result<T>> ExecuteAsync<T>(Func<CancellationToken, Task<Result<T>>> action, CancellationToken cancellationToken = default)
        => ExecuteCoreAsync(action, r => r.IsSuccess, cancellationToken);

    private async Task<TResult> ExecuteCoreAsync<TResult>(
        Func<CancellationToken, Task<TResult>> action,
        Func<TResult, bool> isSuccess,
        CancellationToken cancellationToken)
    {
        // Réentrance : un appel imbriqué rejoint la transaction déjà ouverte.
        if (_ambient.IsActive)
            return await action(cancellationToken);

        await using var rootContext = _contextFactory.CreateIsolatedContext();
        var strategy = rootContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await rootContext.Database.BeginTransactionAsync(cancellationToken);
            _ambient.Activate(
                rootContext.Database.GetDbConnection(),
                transaction.GetDbTransaction());
            try
            {
                var result = await action(cancellationToken);

                if (isSuccess(result))
                {
                    await transaction.CommitAsync(cancellationToken);
                }
                else
                {
                    await transaction.RollbackAsync(cancellationToken);
                    _logger.LogInformation("Unité de travail annulée (Result en échec) — rollback effectué.");
                }

                return result;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
            finally
            {
                _ambient.Clear();
            }
        });
    }
}
