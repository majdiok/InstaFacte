using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Allocation du compte auxiliaire 425 d'un salarié : numérotation séquentielle courte, cohabitation
/// avec les comptes hérités de la dérivation par matricule, et typage auxiliaire.
/// </summary>
public sealed class PayrollEmployeeChartProvisioningTests
{
    private static DbContextOptions<TenantDbContext> NewOptions() =>
        new DbContextOptionsBuilder<TenantDbContext>()
            .UseInMemoryDatabase($"payroll-aux-{Guid.NewGuid():N}")
            .Options;

    private static ChartOfAccount Collective() =>
        ChartOfAccount.Create("425", "Personnel - rémunérations dues", 4, "42",
            AccountNatureType.Credit, isSystem: true).Value;

    /// <summary>
    /// Frère dont le suffixe est plus long que la séquence allouée. Les comptes hérités de la
    /// dérivation du matricule (425 + 7 chiffres) en étaient le cas réel ; ils ne sont plus
    /// créables — la fabrique du domaine plafonne à 8 chiffres — mais un suffixe à 5 chiffres
    /// reproduit exactement la situation à tester.
    /// </summary>
    private static ChartOfAccount LongSuffixSibling(string number) =>
        ChartOfAccount.Create(number, $"Compte hérité — {number}", 4, "425",
            AccountNatureType.Credit).Value;

    private static PayrollEmployeeChartProvisioningService BuildService(DbContextOptions<TenantDbContext> options) =>
        new(new InMemoryContextFactory(options));

    [Fact]
    public async Task Allocate_StartsAtTheFirstSequentialNumber()
    {
        var options = NewOptions();
        await using var ctx = new TenantDbContext(options);
        ctx.ChartOfAccounts.Add(Collective());
        await ctx.SaveChangesAsync();

        var result = await BuildService(options).AllocateAsync("Karim Soumi");

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.Equal("4250001", result.Value);
    }

    [Fact]
    public async Task Allocate_CreatesATypedAuxiliaryNamedAfterTheEmployee()
    {
        var options = NewOptions();
        await using var ctx = new TenantDbContext(options);
        ctx.ChartOfAccounts.Add(Collective());
        await ctx.SaveChangesAsync();

        var number = (await BuildService(options).AllocateAsync("Karim Soumi")).Value;
        var created = await ctx.ChartOfAccounts.SingleAsync(a => a.AccountNumber == number);

        Assert.Equal("Karim Soumi", created.Label);
        Assert.True(created.IsAuxiliary);
        Assert.Equal("425", created.AffectationAccountNumber);
        Assert.Equal("425", created.ParentAccountNumber);
        Assert.Equal(AccountType.Other, created.AccountType);
        Assert.False(created.IsSystem);
        Assert.Equal(4, created.AccountClass);
    }

    [Fact]
    public async Task Allocate_IgnoresSiblingsWithALongerSuffix()
    {
        // « Le plus grand suffixe + 1 » ferait démarrer la séquence à 87 445 — hors du plafond de
        // 8 chiffres dès le premier compte. On repart de 1 en sautant les numéros pris ; un frère
        // au suffixe plus long vit dans un autre espace et ne peut pas entrer en collision.
        var options = NewOptions();
        await using var ctx = new TenantDbContext(options);
        ctx.ChartOfAccounts.AddRange(
            Collective(), LongSuffixSibling("42587444"), LongSuffixSibling("42596555"));
        await ctx.SaveChangesAsync();

        var result = await BuildService(options).AllocateAsync("Nouvelle recrue");

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error.Description : null);
        Assert.Equal("4250001", result.Value);
    }

    [Fact]
    public async Task Allocate_SkipsNumbersAlreadyTaken()
    {
        var options = NewOptions();
        await using var ctx = new TenantDbContext(options);
        ctx.ChartOfAccounts.AddRange(
            Collective(),
            ChartOfAccount.Create("4250001", "Déjà pris", 4, "425", AccountNatureType.Credit).Value);
        await ctx.SaveChangesAsync();

        var result = await BuildService(options).AllocateAsync("Karim Soumi");

        Assert.Equal("4250002", result.Value);
    }

    [Fact]
    public async Task Allocate_IsSequentialAcrossCalls()
    {
        var options = NewOptions();
        await using var ctx = new TenantDbContext(options);
        ctx.ChartOfAccounts.Add(Collective());
        await ctx.SaveChangesAsync();

        var service = BuildService(options);
        var first = await service.AllocateAsync("Alice");
        var second = await service.AllocateAsync("Bob");

        Assert.Equal("4250001", first.Value);
        Assert.Equal("4250002", second.Value);
    }

    [Fact]
    public async Task Allocate_FailsWhenTheCollectiveIsMissing()
    {
        var options = NewOptions();
        await using var ctx = new TenantDbContext(options);

        var result = await BuildService(options).AllocateAsync("Karim Soumi");

        Assert.True(result.IsFailure);
        Assert.Contains("425", result.Error.Description);
    }

    [Fact]
    public async Task Allocate_FallsBackToTheCollectiveLabel_WhenTheNameIsMissing()
    {
        var options = NewOptions();
        await using var ctx = new TenantDbContext(options);
        ctx.ChartOfAccounts.Add(Collective());
        await ctx.SaveChangesAsync();

        var number = (await BuildService(options).AllocateAsync("   ")).Value;
        var created = await ctx.ChartOfAccounts.SingleAsync(a => a.AccountNumber == number);

        Assert.Equal($"Personnel - rémunérations dues — {number}", created.Label);
    }

    /// <summary>
    /// Fabrique un contexte neuf sur la même base en mémoire à chaque appel : le service dispose
    /// le contexte qu'il obtient, un contexte partagé ne survivrait donc pas à la première allocation.
    /// </summary>
    private sealed class InMemoryContextFactory : FactuTrust.Infrastructure.MultiTenancy.ITenantDbContextFactory
    {
        private readonly DbContextOptions<TenantDbContext> _options;
        public InMemoryContextFactory(DbContextOptions<TenantDbContext> options) => _options = options;
        public TenantDbContext CreateContext() => new(_options);
        public TenantDbContext CreateIsolatedContext() => new(_options);
    }
}
