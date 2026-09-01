using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Services.SectorCatalog;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.SectorCatalog;

/// <summary>
/// Plan §1.1, décision D1 — tests de la projection <see cref="SectorCatalogDtoMapper"/> : le flag
/// <c>AvailableOnFreePlan</c> doit être <c>false</c> uniquement pour les modules premium
/// (AI/Forecasting/Studio/Payroll — <see cref="AppModuleExtensions.PaidPlanModuleIds"/>) et
/// <c>true</c> pour tous les autres (cœur + standard), afin que le wizard d'inscription les affiche
/// verrouillés « plan supérieur requis » plutôt que de les refuser silencieusement après l'inscription.
/// </summary>
public sealed class SectorCatalogDtoMapperTests
{
    private static readonly AppModule[] PremiumModules =
    {
        AppModule.AI,
        AppModule.Forecasting,
        AppModule.Studio,
        AppModule.Payroll
    };

    [Fact]
    public void Mapper_sets_AvailableOnFreePlan_false_only_for_premium_modules()
    {
        var dto = SectorCatalogDtoMapper.BuildCatalog(new StaticSectorCatalogProvider().GetSnapshot());

        // Each module: availableOnFreePlan == !isPremium.
        foreach (var module in dto.Modules)
        {
            var isPremium = ((AppModule)module.Id).IsPaidPlanOnly();
            Assert.Equal(!isPremium, module.AvailableOnFreePlan);
        }

        // Sanity: every premium module present is locked, every other is available on Free.
        Assert.All(dto.Modules.Where(m => ((AppModule)m.Id).IsPaidPlanOnly()),
            m => Assert.False(m.AvailableOnFreePlan));
        Assert.All(dto.Modules.Where(m => !((AppModule)m.Id).IsPaidPlanOnly()),
            m => Assert.True(m.AvailableOnFreePlan));

        // Honoraires is excluded from the public catalog entirely (firm-native, never offered).
        Assert.DoesNotContain(dto.Modules, m => m.Id == (int)AppModule.Honoraires);
    }

    [Fact]
    public void PaidPlanModuleIds_constant_covers_exactly_the_four_premium_modules()
    {
        Assert.Equal(PremiumModules, AppModuleExtensions.PaidPlanModuleIds);

        foreach (var premium in PremiumModules)
            Assert.True(premium.IsPaidPlanOnly());

        // Core + standard + Honoraires must NOT be paid-plan-only.
        Assert.False(AppModule.Clients.IsPaidPlanOnly());
        Assert.False(AppModule.Stock.IsPaidPlanOnly());
        Assert.False(AppModule.Fiscal.IsPaidPlanOnly());
        Assert.False(AppModule.Projects.IsPaidPlanOnly());
        Assert.False(AppModule.RecurringContracts.IsPaidPlanOnly());
        Assert.False(AppModule.Honoraires.IsPaidPlanOnly());
    }
}
