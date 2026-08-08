using FactuTrust.Domain.Common;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Persistence;

/// <summary>
/// Garde-fou : la convention <c>PersistenceConventions.ApplyClientGeneratedGuidKeys</c> DOIT
/// couvrir toutes les entités du domaine (dérivés d'<see cref="Entity"/>) portant une PK
/// scalaire <c>Guid</c> nommée <c>Id</c>. Sans elle, le bug d'encaissement honoraires
/// (409 CONCURRENCY_CONFLICT sur insertion via navigation) réapparaîtrait.
/// </summary>
public sealed class ClientGeneratedGuidKeysTests
{
    [Fact]
    public void TenantDbContext_MarksAllDomainGuidKeys_AsValueGeneratedNever()
    {
        AssertConventionAppliedTo(BuildTenantContext());
    }

    [Fact]
    public void MasterDbContext_MarksAllDomainGuidKeys_AsValueGeneratedNever()
    {
        AssertConventionAppliedTo(BuildMasterContext());
    }

    private static void AssertConventionAppliedTo(DbContext context)
    {
        var offenders = context.Model.GetEntityTypes()
            .Where(t => !t.IsOwned())
            .Where(t => typeof(Entity).IsAssignableFrom(t.ClrType))
            .Select(t => new { Type = t, Key = t.FindPrimaryKey() })
            .Where(x => x.Key is not null && x.Key.Properties.Count == 1)
            .Select(x => x.Key!.Properties[0])
            .Where(pk => pk.Name == nameof(Entity.Id) && pk.ClrType == typeof(Guid))
            .Where(pk => pk.ValueGenerated != ValueGenerated.Never)
            .Select(pk => $"{pk.DeclaringType.ClrType.FullName}.Id => {pk.ValueGenerated}")
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "Ces clés Guid.Id d'entités du domaine ne sont PAS marquées ValueGenerated.Never — " +
            "elles réintroduiraient le bug d'encaissement honoraires (409 CONCURRENCY_CONFLICT) : " +
            Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    private static TenantDbContext BuildTenantContext()
    {
        var options = new DbContextOptionsBuilder<TenantDbContext>()
            .UseInMemoryDatabase($"TenantModel_{Guid.NewGuid():N}")
            .Options;
        return new TenantDbContext(options);
    }

    private static MasterDbContext BuildMasterContext()
    {
        var options = new DbContextOptionsBuilder<MasterDbContext>()
            .UseInMemoryDatabase($"MasterModel_{Guid.NewGuid():N}")
            .Options;
        return new MasterDbContext(options);
    }
}
