using FactuTrust.Domain.Enums;
using FactuTrust.Domain.SectorConfiguration;
using FactuTrust.Infrastructure.Services.SectorRules;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.SectorRules;

/// <summary>Plan §2.2 — CompanyModulesController's validation rules (core modules, dependency closure).</summary>
public sealed class CompanyModuleReconfigurationValidatorTests
{
    [Fact]
    public void ValidateCoreModules_returns_null_when_all_core_modules_present()
    {
        var requested = new HashSet<AppModule>(SectorConfigurationCatalog.CoreModules) { AppModule.Stock };
        var error = CompanyModuleReconfigurationValidator.ValidateCoreModules(requested, SectorConfigurationCatalog.CoreModules);
        Assert.Null(error);
    }

    [Fact]
    public void ValidateCoreModules_rejects_missing_core_module_with_French_message()
    {
        var requested = new HashSet<AppModule>(SectorConfigurationCatalog.CoreModules);
        requested.Remove(AppModule.Clients);

        var error = CompanyModuleReconfigurationValidator.ValidateCoreModules(requested, SectorConfigurationCatalog.CoreModules);

        Assert.NotNull(error);
        Assert.Contains("obligatoires", error);
        Assert.Contains(AppModule.Clients.ToDisplayString(), error);
    }

    [Fact]
    public void ValidateDependencies_returns_null_when_no_edges()
    {
        var requested = new HashSet<AppModule> { AppModule.Stock };
        var error = CompanyModuleReconfigurationValidator.ValidateDependencies(requested, Array.Empty<ModuleDependencySnapshot>());
        Assert.Null(error);
    }

    [Fact]
    public void ValidateDependencies_returns_null_when_dependency_satisfied()
    {
        var edges = new[] { new ModuleDependencySnapshot { ModuleId = (int)AppModule.Stock, RequiredModuleId = (int)AppModule.Purchases } };
        var requested = new HashSet<AppModule> { AppModule.Stock, AppModule.Purchases };

        var error = CompanyModuleReconfigurationValidator.ValidateDependencies(requested, edges);
        Assert.Null(error);
    }

    [Fact]
    public void ValidateDependencies_rejects_disabling_a_module_required_by_an_enabled_one()
    {
        var edges = new[] { new ModuleDependencySnapshot { ModuleId = (int)AppModule.Stock, RequiredModuleId = (int)AppModule.Purchases } };
        // Stock requested (enabled) but Purchases (its dependency) is not in the requested set.
        var requested = new HashSet<AppModule> { AppModule.Stock };

        var error = CompanyModuleReconfigurationValidator.ValidateDependencies(requested, edges);

        Assert.NotNull(error);
        Assert.Contains("Requis par", error);
        Assert.Contains(AppModule.Purchases.ToDisplayString(), error);
        Assert.Contains(AppModule.Stock.ToDisplayString(), error);
    }

    [Fact]
    public void ValidateDependencies_allows_disabling_dependency_when_dependent_module_also_disabled()
    {
        var edges = new[] { new ModuleDependencySnapshot { ModuleId = (int)AppModule.Stock, RequiredModuleId = (int)AppModule.Purchases } };
        // Neither Stock nor Purchases requested — disabling both together is fine.
        var requested = new HashSet<AppModule>();

        var error = CompanyModuleReconfigurationValidator.ValidateDependencies(requested, edges);
        Assert.Null(error);
    }
}
