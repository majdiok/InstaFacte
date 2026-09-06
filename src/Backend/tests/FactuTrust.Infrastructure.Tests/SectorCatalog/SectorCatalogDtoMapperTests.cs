using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Services.SectorCatalog;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.SectorCatalog;

/// <summary>
/// Tests de la projection <see cref="SectorCatalogDtoMapper"/> : le flag
/// <c>AvailableOnFreePlan</c> reflète <see cref="AppModuleExtensions.PaidPlanModuleIds"/>.
/// Politique actuelle (2026-09) : liste vide — tous les modules du catalogue public sont
/// disponibles sur le plan Free.
/// </summary>
public sealed class SectorCatalogDtoMapperTests
{
    private static readonly AppModule[] FormerPremiumModules =
    {
        AppModule.AI,
        AppModule.Forecasting,
        AppModule.Studio,
        AppModule.Payroll
    };

    [Fact]
    public void Mapper_sets_AvailableOnFreePlan_true_for_all_catalog_modules_when_no_premium_set()
    {
        var dto = SectorCatalogDtoMapper.BuildCatalog(new StaticSectorCatalogProvider().GetSnapshot());

        foreach (var module in dto.Modules)
        {
            var isPremium = ((AppModule)module.Id).IsPaidPlanOnly();
            Assert.Equal(!isPremium, module.AvailableOnFreePlan);
        }

        Assert.All(dto.Modules, m => Assert.True(m.AvailableOnFreePlan));
        Assert.DoesNotContain(dto.Modules, m => m.AvailableOnFreePlan == false);

        // Honoraires is excluded from the public catalog entirely (firm-native, never offered).
        Assert.DoesNotContain(dto.Modules, m => m.Id == (int)AppModule.Honoraires);
    }

    [Fact]
    public void PaidPlanModuleIds_constant_is_empty_and_former_premium_modules_are_not_paid_plan_only()
    {
        Assert.Empty(AppModuleExtensions.PaidPlanModuleIds);

        foreach (var module in FormerPremiumModules)
            Assert.False(module.IsPaidPlanOnly());

        Assert.False(AppModule.Clients.IsPaidPlanOnly());
        Assert.False(AppModule.Stock.IsPaidPlanOnly());
        Assert.False(AppModule.Fiscal.IsPaidPlanOnly());
        Assert.False(AppModule.Projects.IsPaidPlanOnly());
        Assert.False(AppModule.RecurringContracts.IsPaidPlanOnly());
        Assert.False(AppModule.Honoraires.IsPaidPlanOnly());
    }
}
