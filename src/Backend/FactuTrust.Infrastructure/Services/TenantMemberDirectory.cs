using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Common;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services;

public sealed class TenantMemberDirectory : ITenantMemberDirectory
{
    private readonly MasterDbContext _db;
    private readonly ITenantContext _tenantContext;

    public TenantMemberDirectory(MasterDbContext db, ITenantContext tenantContext)
    {
        _db = db;
        _tenantContext = tenantContext;
    }

    public async Task<Result<(Guid Id, string DisplayName)>> GetMemberAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
            return Result.Failure<(Guid, string)>(Error.Validation("Tenant", "Contexte entreprise introuvable"));

        var row = await _db.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.Id == userId && u.IsActive)
            .Select(u => new { u.Id, u.FirstName, u.LastName, u.Email })
            .FirstOrDefaultAsync(cancellationToken);

        if (row is null)
            return Result.Failure<(Guid, string)>(Error.Validation("AssignedUserId", "Utilisateur introuvable ou inactif."));

        var name = $"{row.FirstName} {row.LastName}".Trim();
        if (string.IsNullOrEmpty(name))
            name = row.Email ?? "Utilisateur";

        return Result.Success((row.Id, name));
    }
}
