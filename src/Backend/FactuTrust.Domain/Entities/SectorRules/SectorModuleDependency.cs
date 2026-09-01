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
}
