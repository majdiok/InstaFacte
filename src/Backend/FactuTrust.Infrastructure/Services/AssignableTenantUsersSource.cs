using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services;

public sealed class AssignableTenantUsersSource : IAssignableTenantUsersSource
{
    private readonly MasterDbContext _db;
    private readonly ITenantContext _tenantContext;

    public AssignableTenantUsersSource(MasterDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<IReadOnlyList<CrmAssignableUserDto>> ListActiveAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
            return Array.Empty<CrmAssignableUserDto>();

        return await ListActiveForTenantAsync(tenantId.Value, cancellationToken);
    }

    public async Task<IReadOnlyList<CrmAssignableUserDto>> ListActiveForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var rows = await _db.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.IsActive)
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
            .ToListAsync(cancellationToken);

        return rows
            .Select(u =>
            {
                var name = $"{u.FirstName} {u.LastName}".Trim();
                if (string.IsNullOrEmpty(name))
                    name = u.Email ?? "Utilisateur";
                return new CrmAssignableUserDto { Id = u.Id, DisplayName = name };
            })
            .ToList();
    }
}
