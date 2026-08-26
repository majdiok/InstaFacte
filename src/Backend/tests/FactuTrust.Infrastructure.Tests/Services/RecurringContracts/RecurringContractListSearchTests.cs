using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services.RecurringContracts;

/// <summary>Recherche liste étendue au nom client (T11/D10).</summary>
/// <remarks>
/// Le provider InMemory évalue <c>Contains</c> en ordinal (sensible à la casse), contrairement
/// à SQL Server (collation insensible par défaut) : les tests utilisent la casse exacte des
/// données seedées. Le comportement « trim + égalité de casse » reste celui d'avant (D10).
/// </remarks>
public sealed class RecurringContractListSearchTests
{
    [Fact]
    public async Task List_SearchByClientName_ReturnsClientContracts()
    {
        var harness = new RecurringContractTestHarness();
        var acme = await harness.SeedClientAsync("Acme Industries");
        var autre = await harness.SeedClientAsync("Autre Société");
        await harness.SeedContractAsync(acme.Id, number: "CTR-AAA-01",
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m));
        await harness.SeedContractAsync(autre.Id, number: "CTR-BBB-02",
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 200m, 19m));

        await using var sut = harness.CreateService();
        var result = await sut.ListAsync(new RecurringContractListQuery { Search = "Acme" });

        Assert.Equal(1, result.TotalCount);
        var item = Assert.Single(result.Items);
        Assert.Equal("CTR-AAA-01", item.Number);
        Assert.Equal("Acme Industries", item.ClientName);
    }

    [Fact]
    public async Task List_SearchByNumber_StillWorks()
    {
        var harness = new RecurringContractTestHarness();
        var acme = await harness.SeedClientAsync("Acme Industries");
        var autre = await harness.SeedClientAsync("Autre Société");
        await harness.SeedContractAsync(acme.Id, number: "CTR-AAA-01",
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m));
        await harness.SeedContractAsync(autre.Id, number: "CTR-BBB-02",
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 200m, 19m));

        await using var sut = harness.CreateService();
        var result = await sut.ListAsync(new RecurringContractListQuery { Search = "BBB-02" });

        // Non-régression : la recherche par numéro conserve son comportement.
        Assert.Equal(1, result.TotalCount);
        Assert.Equal("CTR-BBB-02", Assert.Single(result.Items).Number);
    }

    [Fact]
    public async Task List_SearchMatchingNumberAndClientName_NoDuplicates()
    {
        var harness = new RecurringContractTestHarness();
        var acme = await harness.SeedClientAsync("Acme Corp");
        // Le numéro contient AUSSI le terme → prédicat OR sur la même ligne, pas de doublon.
        await harness.SeedContractAsync(acme.Id, number: "CTR-Acme-1",
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m));

        await using var sut = harness.CreateService();
        var result = await sut.ListAsync(new RecurringContractListQuery { Search = "Acme" });

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task List_SearchCaseAndTrim_BehavesAsBefore()
    {
        var harness = new RecurringContractTestHarness();
        var acme = await harness.SeedClientAsync("Acme Industries");
        await harness.SeedContractAsync(acme.Id, number: "CTR-AAA-01",
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m));

        await using var sut = harness.CreateService();

        // Espaces de part et d'autre → trimmés.
        var trimmed = await sut.ListAsync(new RecurringContractListQuery { Search = "  Acme  " });
        Assert.Equal(1, trimmed.TotalCount);

        // Casse différente : SQL Server matcherait (collation insensible), InMemory non —
        // l'écart est documenté (voir remarque de classe) ; l'important est l'absence d'erreur.
        var wrongCase = await sut.ListAsync(new RecurringContractListQuery { Search = "acme" });
        Assert.Equal(0, wrongCase.TotalCount);

        // Sans terme → liste complète inchangée.
        var all = await sut.ListAsync(new RecurringContractListQuery());
        Assert.Equal(1, all.TotalCount);
    }
}
