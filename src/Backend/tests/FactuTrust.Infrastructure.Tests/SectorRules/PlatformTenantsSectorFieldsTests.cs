using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.SectorConfiguration;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.SectorRules;

/// <summary>
/// Phase 2 — sector classification propagation on platform tenant DTOs (plan §WP-B8, D8): the list
/// and detail projections must expose <c>CompanySegment</c>/<c>BusinessDomain</c> from the tenant
/// row. InMemory-backed so the assertion runs without SQL Server.
/// </summary>
public sealed class PlatformTenantsSectorFieldsTests
{
    private static MasterDbContext NewDb() =>
        new(new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<Tenant> SeedTenantAsync(MasterDbContext db, string? segment, string? domain)
    {
        var n = Random.Shared.Next(1000000, 9999999);
        var office = Random.Shared.Next(0, 999).ToString("000");
        var tenant = Tenant.Create(
            $"Platform-Sector-{n}",
            NIF.Create($"{n}/A/B/C/{office}").Value,
            Address.Create("1 rue Test", "Tunis", "Tunis").Value,
            Email.Create($"platform-sector-{n}@example.com").Value,
            PhoneNumber.Create("20123456").Value,
            TaxRegime.RealRegime).Value;
        tenant.SetSectorClassification(segment, domain);
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();
        return tenant;
    }

    [Fact]
    public async Task Tenant_list_and_detail_expose_sector_classification()
    {
        await using var db = NewDb();
        var tenant = await SeedTenantAsync(db, CompanySegments.Commerce, BusinessDomains.Autre);
        // A second legacy tenant with no classification — its fields must come back null.
        var legacyTenant = await SeedTenantAsync(db, segment: null, domain: null);

        var service = new PlatformTenantQueryService(db);

        var page = await service.ListAsync(new PlatformTenantListQuery { Page = 1, PageSize = 50 }, CancellationToken.None);
        var byId = page.Items.ToDictionary(i => i.TenantId);

        Assert.Equal(CompanySegments.Commerce, byId[tenant.Id].CompanySegment);
        Assert.Equal(BusinessDomains.Autre, byId[tenant.Id].BusinessDomain);
        Assert.Null(byId[legacyTenant.Id].CompanySegment);
        Assert.Null(byId[legacyTenant.Id].BusinessDomain);

        var detail = await service.GetDetailAsync(tenant.Id, CancellationToken.None);
        Assert.NotNull(detail);
        Assert.Equal(CompanySegments.Commerce, detail!.CompanySegment);
        Assert.Equal(BusinessDomains.Autre, detail.BusinessDomain);

        var legacyDetail = await service.GetDetailAsync(legacyTenant.Id, CancellationToken.None);
        Assert.NotNull(legacyDetail);
        Assert.Null(legacyDetail!.CompanySegment);
        Assert.Null(legacyDetail.BusinessDomain);
    }
}
