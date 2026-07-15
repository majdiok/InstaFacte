using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Billing;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services;

/// <summary>Lot C1 — Implémentation EF Core des overrides de modules par tenant.</summary>
public sealed class TenantModuleOverrideService : ITenantModuleOverrideService
{
    private readonly MasterDbContext _db;

    public TenantModuleOverrideService(MasterDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<TenantModuleOverrideDto>> ListByTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var rows = await _db.TenantModuleOverrides
            .AsNoTracking()
            .Where(o => o.TenantId == tenantId)
            .OrderBy(o => o.Module)
            .ToListAsync(cancellationToken);

        return rows.Select(Map).ToList();
    }

    public async Task<Result<TenantModuleOverrideDto>> SetAsync(
        Guid tenantId,
        SetTenantModuleOverrideRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(typeof(AppModule), request.Module))
            return Result.Failure<TenantModuleOverrideDto>(Error.Validation("Module", "Module inconnu."));

        var existing = await _db.TenantModuleOverrides
            .FirstOrDefaultAsync(o => o.TenantId == tenantId && o.Module == request.Module, cancellationToken);

        TenantModuleOverride entity;
        if (existing is null)
        {
            entity = TenantModuleOverride.Create(
                tenantId,
                request.Module,
                request.IsEnabled,
                actorUserId,
                request.ExpiresAt,
                request.Reason);
            _db.TenantModuleOverrides.Add(entity);
        }
        else
        {
            existing.UpdateState(request.IsEnabled, request.ExpiresAt, request.Reason);
            entity = existing;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return Result.Success(Map(entity));
    }

    public async Task<bool> RemoveAsync(Guid overrideId, CancellationToken cancellationToken = default)
    {
        var entity = await _db.TenantModuleOverrides.FirstOrDefaultAsync(o => o.Id == overrideId, cancellationToken);
        if (entity is null) return false;
        _db.TenantModuleOverrides.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> RemoveExpiredAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        return await _db.TenantModuleOverrides
            .Where(o => o.ExpiresAt != null && o.ExpiresAt <= nowUtc)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static TenantModuleOverrideDto Map(TenantModuleOverride o) => new()
    {
        Id = o.Id,
        TenantId = o.TenantId,
        Module = o.Module,
        ModuleDisplay = Enum.IsDefined(typeof(AppModule), o.Module)
            ? ((AppModule)o.Module).ToDisplayString()
            : $"Module #{o.Module}",
        IsEnabled = o.IsEnabled,
        ExpiresAt = o.ExpiresAt,
        GrantedByUserId = o.GrantedByUserId,
        Reason = o.Reason,
        IsCurrentlyActive = o.IsCurrentlyActive
    };
}
