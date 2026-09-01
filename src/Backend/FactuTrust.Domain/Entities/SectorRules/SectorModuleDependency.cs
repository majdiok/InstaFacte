using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities.SectorRules;

/// <summary>
/// Master-DB backed module dependency edge (Phase 2, plan §WP-B4/§WP-B5, §4.2): selecting
/// <see cref="ModuleId"/> auto-pulls <see cref="RequiredModuleId"/>. The static catalog declares a
/// small approved set (<c>SectorConfigurationCatalog.ModuleDependencies</c>), seeded by
/// <c>SectorRuleSeeder</c>; additional edges can be authored via the admin CRUD. Acyclicity is
/// enforced at the service layer (<c>SectorRuleAdminService</c>), never here.
/// </summary>
public sealed class SectorModuleDependency : Entity
{
    public int ModuleId { get; private set; }
    public int RequiredModuleId { get; private set; }
    public bool IsActive { get; private set; } = true;

    /// <summary>
    /// True when this row is owned by the catalog seeder. Startup reconciliation only ever
    /// refreshes/reactivates/deactivates catalog-owned edges; an admin edit flips this to false.
    /// </summary>
    public bool IsManagedByCatalog { get; private set; } = true;

    private SectorModuleDependency() { }

    public static SectorModuleDependency Create(int moduleId, int requiredModuleId)
    {
        return new SectorModuleDependency
        {
            ModuleId = moduleId,
            RequiredModuleId = requiredModuleId,
            IsActive = true
        };
    }

    public void Deactivate() => IsActive = false;

    public void Reactivate() => IsActive = true;

    /// <summary>Marks the row as admin-authored/edited so startup reconciliation never overwrites it.</summary>
    public void MarkAdminManaged() => IsManagedByCatalog = false;

    /// <summary>Reclaims the row as catalog-owned (used by a factory-reset force seed).</summary>
    public void MarkCatalogManaged() => IsManagedByCatalog = true;
}
