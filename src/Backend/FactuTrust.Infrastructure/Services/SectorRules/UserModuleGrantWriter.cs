using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services.SectorRules;

/// <summary>
/// Shared <c>UserModuleGrant</c> rewrite logic (plan §2.2) — extracted out of
/// <c>RegistrationSectorService.ApplyModuleSelectionAsync</c> so the exact same
/// delete-then-insert/"all modules = no rows" semantics can be reused by
/// <c>TenantSectorReconfigurationService</c> and the new <c>CompanyModulesController</c>
/// without duplicating the logic a third time.
/// </summary>
public static class UserModuleGrantWriter
{
    /// <summary>
    /// Rewrites <paramref name="userId"/>'s <c>UserModuleGrant</c> rows to the canonical
    /// restriction-only form: deletes any pre-existing rows, then inserts one row per module with
    /// <c>IsEnabled</c> reflecting <paramref name="finalSet"/> — or nothing when the full set equals
    /// all modules (legacy "all enabled" representation, consumed by
    /// <c>EffectivePermissionService.ResolveEnabledModules</c>). A brand-new user with no existing
    /// rows behaves identically (the delete is a no-op). Never touches
    /// <c>TenantModuleOverrides</c>, plans or role assignments.
    /// </summary>
    /// <param name="saveChanges">
    /// When true, calls <c>SaveChangesAsync</c> once after staging the delete+insert for this user
    /// (single-user callers). Batch callers looping over many users should pass false and issue one
    /// <c>SaveChangesAsync</c> after the loop.
    /// </param>
    public static async Task RewriteGrantsAsync(
        MasterDbContext db,
        Guid userId,
        HashSet<AppModule> finalSet,
        bool saveChanges,
        CancellationToken cancellationToken)
    {
        var existing = await db.UserModuleGrants.Where(g => g.UserId == userId).ToListAsync(cancellationToken);
        if (existing.Count > 0)
            db.UserModuleGrants.RemoveRange(existing);

        if (finalSet.Count != AppModuleExtensions.AllValues.Length)
        {
            foreach (var module in AppModuleExtensions.AllValues)
            {
                db.UserModuleGrants.Add(new Domain.Entities.UserModuleGrant
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Module = module,
                    IsEnabled = finalSet.Contains(module),
                    EnabledFeatureKeys = null
                });
            }
        }

        if (saveChanges)
            await db.SaveChangesAsync(cancellationToken);
    }
}
