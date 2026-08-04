using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Infrastructure.Persistence;

/// <summary>
/// Interceptor de diagnostic activé UNIQUEMENT en développement pour tracer
/// toute commande EF Core exécutée sur <c>MasterDbContext</c> avec son ThreadId,
/// son ActivityId et l'identifiant du DbContext qui l'a portée.
/// <para>
/// Objectif : quand une <see cref="InvalidOperationException"/> « A second operation
/// was started on this context instance… » se produit à nouveau, le journal
/// contient déjà la séquence des commandes précédentes sur la même instance
/// de contexte, avec threads et activités, permettant d'identifier immédiatement
/// les deux appelants concurrents.
/// </para>
/// <para>
/// Aucun effet en production (non enregistré). Aucun impact fonctionnel : ne modifie
/// jamais les résultats de commande, ne throw pas, n'attend aucune ressource externe.
/// </para>
/// </summary>
public sealed class MasterDbConcurrencyDiagnosticsInterceptor : DbCommandInterceptor
{
    private readonly ILogger<MasterDbConcurrencyDiagnosticsInterceptor> _logger;

    // Compteur d'exécutions concurrentes par ContextId : incrémenté sur ExecutingXxx,
    // décrémenté sur ExecutedXxx / FailedXxx. Une valeur > 1 = concurrence détectée
    // (avant même qu'EF ne lève l'exception).
    private static readonly ConcurrentDictionary<Guid, int> InFlightPerContext = new();

    public MasterDbConcurrencyDiagnosticsInterceptor(ILogger<MasterDbConcurrencyDiagnosticsInterceptor> logger)
    {
        _logger = logger;
    }

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        BeginTrace(eventData, command, sync: true);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        BeginTrace(eventData, command, sync: false);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override DbDataReader ReaderExecuted(
        DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    {
        EndTrace(eventData);
        return base.ReaderExecuted(command, eventData, result);
    }

    public override ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        EndTrace(eventData);
        return base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
    {
        BeginTrace(eventData, command, sync: true);
        return base.NonQueryExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        BeginTrace(eventData, command, sync: false);
        return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override int NonQueryExecuted(DbCommand command, CommandExecutedEventData eventData, int result)
    {
        EndTrace(eventData);
        return base.NonQueryExecuted(command, eventData, result);
    }

    public override ValueTask<int> NonQueryExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        EndTrace(eventData);
        return base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    {
        BeginTrace(eventData, command, sync: true);
        return base.ScalarExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        BeginTrace(eventData, command, sync: false);
        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override object? ScalarExecuted(
        DbCommand command, CommandExecutedEventData eventData, object? result)
    {
        EndTrace(eventData);
        return base.ScalarExecuted(command, eventData, result);
    }

    public override ValueTask<object?> ScalarExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, object? result,
        CancellationToken cancellationToken = default)
    {
        EndTrace(eventData);
        return base.ScalarExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override void CommandFailed(DbCommand command, CommandErrorEventData eventData)
    {
        EndTrace(eventData);
        base.CommandFailed(command, eventData);
    }

    public override Task CommandFailedAsync(
        DbCommand command, CommandErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        EndTrace(eventData);
        return base.CommandFailedAsync(command, eventData, cancellationToken);
    }

    private void BeginTrace(CommandEventData eventData, DbCommand command, bool sync)
    {
        var contextId = eventData.Context?.ContextId.InstanceId ?? Guid.Empty;
        var inFlight = InFlightPerContext.AddOrUpdate(contextId, 1, (_, v) => v + 1);

        if (inFlight > 1)
        {
            _logger.LogError(
                "DBCONTEXT_CONCURRENCY_DETECTED before EF throws: contextId={ContextId} inFlight={InFlight} " +
                "thread={ThreadId} activity={ActivityId} sync={Sync} sql={Sql} — stack: {Stack}",
                contextId,
                inFlight,
                Environment.CurrentManagedThreadId,
                Activity.Current?.Id ?? "(none)",
                sync,
                Truncate(command.CommandText, 240),
                Environment.StackTrace);
            return;
        }

        _logger.LogDebug(
            "MasterDb command begin: contextId={ContextId} thread={ThreadId} activity={ActivityId} sync={Sync} sql={Sql}",
            contextId,
            Environment.CurrentManagedThreadId,
            Activity.Current?.Id ?? "(none)",
            sync,
            Truncate(command.CommandText, 160));
    }

    private static void EndTrace(CommandEventData eventData)
    {
        var contextId = eventData.Context?.ContextId.InstanceId ?? Guid.Empty;
        InFlightPerContext.AddOrUpdate(contextId, 0, (_, v) => Math.Max(0, v - 1));
    }

    private static string Truncate(string value, int max)
        => string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max] + "…";
}
