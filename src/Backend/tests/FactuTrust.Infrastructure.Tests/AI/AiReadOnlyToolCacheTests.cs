using System;
using System.Collections.Generic;
using FactuTrust.Application.Common;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.AI.DTOs;
using FactuTrust.Application.Features.AI.Tools;
using FactuTrust.Domain.Enums;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.AI;

public sealed class AiReadOnlyToolCacheTests
{
    // `enabled` pilote le cache TENANT (EnableReadOnlyToolCache) ; `firmEnabled` pilote le cache FIRM
    // (FirmMissionReadOnlyToolCacheEnabled) — flags dédiés et indépendants (Lot 3.3).
    private static AiReadOnlyToolCache CreateCache(bool enabled = true, bool firmEnabled = true) =>
        new(new MemoryCache(new MemoryCacheOptions()), Options.Create(new OllamaSettings
        {
            EnableReadOnlyToolCache = enabled,
            FirmMissionReadOnlyToolCacheEnabled = firmEnabled,
            ReadOnlyToolCacheSeconds = 45
        }));

    private static Dictionary<string, object?> EmptyArgs() => new();

    [Fact]
    public void IsCacheable_ReadOnlyDbTool_ReturnsTrue()
    {
        var cache = CreateCache();
        Assert.True(cache.IsCacheable("get_sales_revenue"));
        Assert.False(cache.IsCacheable("create_product"));
    }

    [Fact]
    public void TryGet_AfterSet_ReturnsHitForSameTenantAndArgs()
    {
        var cache = CreateCache();
        var args = new Dictionary<string, object?> { ["from_date"] = "2026-03-01", ["to_date"] = "2026-03-31" };
        var result = AiToolResult.Ok("{\"rows\":[]}");

        cache.Set("tenant-a", "get_sales_revenue", args, result);

        Assert.True(cache.TryGet("tenant-a", "get_sales_revenue", args, out var hit));
        Assert.True(hit.Success);
        Assert.False(cache.TryGet("tenant-b", "get_sales_revenue", args, out _));
    }

    [Fact]
    public void IsCacheable_WhenDisabled_ReturnsFalse()
    {
        var cache = CreateCache(enabled: false);
        Assert.False(cache.IsCacheable("get_sales_revenue"));
    }

    // ── Lot 3.3 — cache firm dédié (clé user-scopée, réservé FirmManager) ──

    [Fact]
    public void FirmManager_SetThenTryGet_ReturnsHit()
    {
        var cache = CreateCache();
        var scope = FirmDossierAccessScope.ForUser(Guid.NewGuid(), UserRole.FirmManager);
        var args = EmptyArgs();

        Assert.True(cache.IsCacheable(FirmAgentTools.PortfolioOverview, scope));
        cache.Set("tenant-a", FirmAgentTools.PortfolioOverview, args, AiToolResult.Ok("{}"), scope);

        Assert.True(cache.TryGet("tenant-a", FirmAgentTools.PortfolioOverview, args, out var hit, scope));
        Assert.True(hit.Success);
    }

    [Fact]
    public void FirmAccountant_NeverCached_EvenWithFlagOn()
    {
        var cache = CreateCache();
        var scope = FirmDossierAccessScope.ForUser(Guid.NewGuid(), UserRole.FirmAccountant);
        var args = EmptyArgs();

        // Décision 3.3 : FirmAccountant n'est JAMAIS caché inter-requêtes (ACL par affectation).
        Assert.False(cache.IsCacheable(FirmAgentTools.PortfolioOverview, scope));

        // Set est un no-op (IsCacheable false) : aucune entrée stockée, donc aucun hit ultérieur.
        cache.Set("tenant-a", FirmAgentTools.PortfolioOverview, args, AiToolResult.Ok("{}"), scope);
        Assert.False(cache.TryGet("tenant-a", FirmAgentTools.PortfolioOverview, args, out _, scope));
    }

    [Fact]
    public void FirmFlagOff_NoFirmCache_TenantCacheIntact()
    {
        var cache = CreateCache(firmEnabled: false);
        var firmScope = FirmDossierAccessScope.ForUser(Guid.NewGuid(), UserRole.FirmManager);

        // Cache firm désactivé par son PROPRE flag — sans toucher au cache tenant.
        Assert.False(cache.IsCacheable(FirmAgentTools.PortfolioOverview, firmScope));
        cache.Set("tenant-a", FirmAgentTools.PortfolioOverview, EmptyArgs(), AiToolResult.Ok("{}"), firmScope);
        Assert.False(cache.TryGet("tenant-a", FirmAgentTools.PortfolioOverview, EmptyArgs(), out _, firmScope));

        // Cache tenant intact (flag tenant toujours actif).
        Assert.True(cache.IsCacheable("get_sales_revenue"));
        var args = new Dictionary<string, object?> { ["preset"] = "today" };
        cache.Set("tenant-a", "get_sales_revenue", args, AiToolResult.Ok("{\"total\":1}"));
        Assert.True(cache.TryGet("tenant-a", "get_sales_revenue", args, out var tenantHit));
        Assert.True(tenantHit.Success);
    }

    [Fact]
    public void TenantCacheOff_FirmCacheStillActive_WhenFirmFlagOn()
    {
        // Réciproque du précédent : un rollback du cache TENANT ne désactive pas le cache FIRM.
        var cache = CreateCache(enabled: false, firmEnabled: true);

        Assert.False(cache.IsCacheable("get_sales_revenue")); // tenant off
        var firmScope = FirmDossierAccessScope.ForUser(Guid.NewGuid(), UserRole.FirmManager);
        Assert.True(cache.IsCacheable(FirmAgentTools.PortfolioOverview, firmScope)); // firm on
    }

    [Fact]
    public void FirmManager_NoCrossUserHits()
    {
        var cache = CreateCache();
        var scope1 = FirmDossierAccessScope.ForUser(Guid.NewGuid(), UserRole.FirmManager);
        var scope2 = FirmDossierAccessScope.ForUser(Guid.NewGuid(), UserRole.FirmManager);
        var args = EmptyArgs();

        cache.Set("tenant-a", FirmAgentTools.PortfolioOverview, args, AiToolResult.Ok("{}"), scope1);

        // Clé user-scopée TenantId:UserId:Role ⇒ aucun hit croisé entre deux FirmManager du même cabinet.
        Assert.False(cache.TryGet("tenant-a", FirmAgentTools.PortfolioOverview, args, out _, scope2));
        Assert.True(cache.TryGet("tenant-a", FirmAgentTools.PortfolioOverview, args, out _, scope1));
    }

    [Fact]
    public void FirmManager_NoCrossTenantHits()
    {
        var cache = CreateCache();
        var scope = FirmDossierAccessScope.ForUser(Guid.NewGuid(), UserRole.FirmManager);
        var args = EmptyArgs();

        cache.Set("tenant-a", FirmAgentTools.PortfolioOverview, args, AiToolResult.Ok("{}"), scope);
        Assert.False(cache.TryGet("tenant-b", FirmAgentTools.PortfolioOverview, args, out _, scope));
        Assert.True(cache.TryGet("tenant-a", FirmAgentTools.PortfolioOverview, args, out _, scope));
    }

    [Fact]
    public void SendReminder_NeverCacheable_EvenForFirmManager()
    {
        var cache = CreateCache();
        var scope = FirmDossierAccessScope.ForUser(Guid.NewGuid(), UserRole.FirmManager);
        var args = EmptyArgs();

        // send_fiscal_deadline_reminder est une mutation, absente de FirmReadOnlyDbToolNames.
        Assert.False(cache.IsCacheable(FirmAgentTools.SendReminder, scope));
        cache.Set("tenant-a", FirmAgentTools.SendReminder, args, AiToolResult.Ok("{}"), scope);
        Assert.False(cache.TryGet("tenant-a", FirmAgentTools.SendReminder, args, out _, scope));
    }

    [Fact]
    public void FirmTool_WithoutScope_NotCacheable()
    {
        var cache = CreateCache();
        // Sans scope (appel tenant historique), un outil firm n'est jamais cacheable via le chemin tenant.
        Assert.False(cache.IsCacheable(FirmAgentTools.PortfolioOverview));
        Assert.False(cache.TryGet("tenant-a", FirmAgentTools.PortfolioOverview, EmptyArgs(), out _));
    }

    [Fact]
    public void Set_DoesNotCacheUnsuccessfulFirmResult()
    {
        var cache = CreateCache();
        var scope = FirmDossierAccessScope.ForUser(Guid.NewGuid(), UserRole.FirmManager);
        var args = EmptyArgs();

        cache.Set("tenant-a", FirmAgentTools.PortfolioOverview, args, AiToolResult.Error("fan-out en échec"), scope);
        Assert.False(cache.TryGet("tenant-a", FirmAgentTools.PortfolioOverview, args, out _, scope));
    }
}
