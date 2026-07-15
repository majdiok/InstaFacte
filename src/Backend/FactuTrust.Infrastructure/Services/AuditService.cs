using System.Data;
using System.Text.Json;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Entities;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Audit logging service with hash chain integrity.
/// </summary>
public sealed class AuditService : IAuditService
{
    /// <summary>
    /// In-memory and other non-relational providers do not support transactions; serialize writes so tests and dev DBs keep a valid chain.
    /// </summary>
    private static readonly SemaphoreSlim NonRelationalAuditGate = new(1, 1);

    private readonly ITenantDbContextFactory _contextFactory;
    private readonly ICurrentUser _currentUser;

    public AuditService(ITenantDbContextFactory contextFactory, ICurrentUser currentUser)
    {
        _contextFactory = contextFactory;
        _currentUser = currentUser;
    }

    public async Task LogAsync(
        string action,
        string entityType,
        Guid? entityId = null,
        object? oldValues = null,
        object? newValues = null,
        CancellationToken cancellationToken = default)
    {
        await using var strategyContext = _contextFactory.CreateContext();
        var relational = strategyContext.Database.IsRelational();
        if (!relational)
            await NonRelationalAuditGate.WaitAsync(cancellationToken);
        try
        {
            if (relational)
            {
                var strategy = strategyContext.Database.CreateExecutionStrategy();
                await strategy.ExecuteAsync(async () =>
                {
                    await using var context = _contextFactory.CreateContext();
                    await using var transaction = await context.Database.BeginTransactionAsync(
                        IsolationLevel.Serializable,
                        cancellationToken);
                    try
                    {
                        await AppendAuditLogAsync(context, action, entityType, entityId, oldValues, newValues, cancellationToken);
                        await transaction.CommitAsync(cancellationToken);
                    }
                    catch
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        throw;
                    }
                });
            }
            else
            {
                await using var context = _contextFactory.CreateContext();
                await AppendAuditLogAsync(context, action, entityType, entityId, oldValues, newValues, cancellationToken);
            }
        }
        finally
        {
            if (!relational)
                NonRelationalAuditGate.Release();
        }
    }

    private async Task AppendAuditLogAsync(
        TenantDbContext context,
        string action,
        string entityType,
        Guid? entityId,
        object? oldValues,
        object? newValues,
        CancellationToken cancellationToken)
    {
        var previousHash = await GetLastHashCoreAsync(context, cancellationToken);

        var auditLog = AuditLog.Create(
            tenantId: _currentUser.TenantId,
            userId: _currentUser.UserId,
            userEmail: _currentUser.Email ?? "system",
            action: action,
            entityType: entityType,
            entityId: entityId,
            oldValues: oldValues != null ? JsonSerializer.Serialize(oldValues) : null,
            newValues: newValues != null ? JsonSerializer.Serialize(newValues) : null,
            ipAddress: _currentUser.IpAddress ?? "unknown",
            userAgent: _currentUser.UserAgent,
            previousHash: previousHash);

        context.AuditLogs.Add(auditLog);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<string> GetLastHashAsync(CancellationToken cancellationToken = default)
    {
        await using var context = _contextFactory.CreateContext();
        return await GetLastHashCoreAsync(context, cancellationToken);
    }

    /// <summary>
    /// Last chain entry by canonical order (CreatedAt, Id), matching verification.
    /// </summary>
    private static async Task<string> GetLastHashCoreAsync(TenantDbContext context, CancellationToken cancellationToken)
    {
        var lastHash = await context.AuditLogs
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.Id)
            .Select(a => a.Hash)
            .FirstOrDefaultAsync(cancellationToken);

        return lastHash ?? "GENESIS";
    }
}
