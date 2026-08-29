using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.Accounting.Commands;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

/// <summary>
/// Correction de masse par extourne. La commande est une PURE DÉLÉGATION : les gardes comptables
/// (flag, brouillon, déjà extournée, miroir débit/crédit) sont prouvées par
/// <see cref="Services.AccountingServiceReversalTests"/> et héritées telles quelles. Ces tests
/// couvrent la logique PROPRE de la commande — comptage, motif obligatoire, dédoublonnage — et
/// vérifient de bout en bout, avec le VRAI service, qu'un brouillon est ignoré et laissé intact.
/// </summary>
public sealed class MassReversalCommandsTests
{
    private static IReadOnlyList<JournalLineInput> SaleLines() => new[]
    {
        new JournalLineInput("4111", "Client", 100m, 0, null, ThirdPartyKind.None),
        new JournalLineInput("707", "Ventes", 0, 100m, null, ThirdPartyKind.None)
    };

    private static IReadOnlyList<JournalLineInput> PurchaseLines() => new[]
    {
        new JournalLineInput("607", "Achats", 60m, 0, null, ThirdPartyKind.None),
        new JournalLineInput("4011", "Fournisseur", 0, 60m, null, ThirdPartyKind.None)
    };

    private static JournalEntry Entry(int number, JournalEntryStatus status,
        IReadOnlyList<JournalLineInput>? lines = null) =>
        JournalEntry.Create(number, "JV", new DateTime(2026, 4, 15), "Vente", Guid.NewGuid(),
            false, "Invoice", Guid.NewGuid(), lines ?? SaleLines(), Money.DefaultCurrency, null, status).Value;

    private static MassReverseEntriesCommandHandler BuildHandler(IAccountingService service)
        => new(service, new Mock<IAuditService>().Object);

    /// <summary>Service simulé : succès pour les ids connus, échec sinon (comme les gardes réelles).</summary>
    private static IAccountingService FakeService(params Guid[] reversibleIds)
    {
        var mock = new Mock<IAccountingService>();
        mock.Setup(x => x.ReverseJournalEntryAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, string _, CancellationToken _) =>
                reversibleIds.Contains(id)
                    ? Result.Success(Guid.NewGuid())
                    : Result.Failure<Guid>(Error.Validation("Extourne", "Refusée.")));
        return mock.Object;
    }

    // ── Logique propre de la commande ──────────────────────────────────────────

    [Fact]
    public async Task Reverses_All_WhenAllEligible()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var result = await BuildHandler(FakeService(a, b))
            .Handle(new MassReverseEntriesCommand(new[] { a, b }, "erreur de saisie"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Reversed);
        Assert.Equal(0, result.Value.Skipped);
        Assert.Equal(2, result.Value.ReversalEntryIds.Count);
    }

    [Fact]
    public async Task CountsIneligibleAsSkipped_WithoutAbortingTheBatch()
    {
        var ok = Guid.NewGuid();
        var refused = Guid.NewGuid();

        var result = await BuildHandler(FakeService(ok))
            .Handle(new MassReverseEntriesCommand(new[] { ok, refused }, "correction"), CancellationToken.None);

        Assert.True(result.IsSuccess);          // un refus n'interrompt pas le lot
        Assert.Equal(1, result.Value.Reversed);
        Assert.Equal(1, result.Value.Skipped);
    }

    [Fact]
    public async Task DuplicateIds_AreProcessedOnce()
    {
        var a = Guid.NewGuid();

        var result = await BuildHandler(FakeService(a))
            .Handle(new MassReverseEntriesCommand(new[] { a, a, a }, "correction"), CancellationToken.None);

        Assert.Equal(1, result.Value.Reversed);
        Assert.Equal(0, result.Value.Skipped);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task MissingReason_IsRefused(string reason)
    {
        var result = await BuildHandler(FakeService(Guid.NewGuid()))
            .Handle(new MassReverseEntriesCommand(new[] { Guid.NewGuid() }, reason), CancellationToken.None);

        Assert.True(result.IsFailure);   // une correction comptable doit être justifiée
    }

    [Fact]
    public async Task EmptySelection_IsRefused()
    {
        var result = await BuildHandler(FakeService())
            .Handle(new MassReverseEntriesCommand(Array.Empty<Guid>(), "correction"), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    // ── Bout en bout avec le VRAI service : gel du brouillon ────────────────────

    [Fact]
    public async Task RealService_DraftIsSkippedAndLeftIntact()
    {
        var draft = Entry(10, JournalEntryStatus.Brouillon);
        var validated = Entry(11, JournalEntryStatus.Validee);

        var journals = new Mock<IJournalEntryRepository>();
        journals.Setup(x => x.GetByIdAsync(draft.Id, It.IsAny<CancellationToken>())).ReturnsAsync(draft);
        journals.Setup(x => x.GetByIdAsync(validated.Id, It.IsAny<CancellationToken>())).ReturnsAsync(validated);
        journals.Setup(x => x.ReserveNextEntryNumberAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(99);
        journals.Setup(x => x.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JournalEntry e, CancellationToken _) => e);

        var periodService = new Mock<IAccountingPeriodService>();
        var period = AccountingPeriod.Create(2026, 4, new DateTime(2026, 4, 1), new DateTime(2026, 4, 30));
        periodService.Setup(x => x.EnsureOpenPeriodAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(period));

        var service = new AccountingService(
            new Mock<IChartOfAccountRepository>().Object, periodService.Object, journals.Object,
            new Mock<IWithholdingTaxRepository>().Object, new Mock<IDepreciationRateCategoryRepository>().Object,
            new Mock<ITenantDbContextFactory>().Object,
            NullLogger<AccountingService>.Instance,
            Options.Create(new AccountingSettings { ManualReversalEnabled = true }));

        var result = await BuildHandler(service)
            .Handle(new MassReverseEntriesCommand(new[] { draft.Id, validated.Id }, "correction"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Reversed);
        Assert.Equal(1, result.Value.Skipped);

        // Le brouillon n'a été ni extourné ni modifié ; la validée porte bien sa marque d'extourne.
        Assert.False(draft.IsReversed);
        Assert.Equal(JournalEntryStatus.Brouillon, draft.Status);
        Assert.True(validated.IsReversed);
    }

    /// <summary>
    /// Articulation comptable : la correction de masse est NEUTRE. Compte par compte, la somme des
    /// mouvements d'origine et de leurs extournes vaut exactement zéro — donc la balance revient à
    /// son état antérieur sur les comptes touchés. C'est la preuve que la correction est complète.
    /// </summary>
    [Fact]
    public async Task RealService_OriginPlusReversal_NetsToZeroPerAccount()
    {
        var sale = Entry(20, JournalEntryStatus.Validee);
        var purchase = Entry(21, JournalEntryStatus.Validee, PurchaseLines());
        var reversals = new List<JournalEntry>();

        var journals = new Mock<IJournalEntryRepository>();
        journals.Setup(x => x.GetByIdAsync(sale.Id, It.IsAny<CancellationToken>())).ReturnsAsync(sale);
        journals.Setup(x => x.GetByIdAsync(purchase.Id, It.IsAny<CancellationToken>())).ReturnsAsync(purchase);
        journals.Setup(x => x.ReserveNextEntryNumberAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(99);
        journals.Setup(x => x.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()))
            .Callback<JournalEntry, CancellationToken>((e, _) => reversals.Add(e))
            .ReturnsAsync((JournalEntry e, CancellationToken _) => e);

        var periodService = new Mock<IAccountingPeriodService>();
        var period = AccountingPeriod.Create(2026, 4, new DateTime(2026, 4, 1), new DateTime(2026, 4, 30));
        periodService.Setup(x => x.EnsureOpenPeriodAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(period));

        var service = new AccountingService(
            new Mock<IChartOfAccountRepository>().Object, periodService.Object, journals.Object,
            new Mock<IWithholdingTaxRepository>().Object, new Mock<IDepreciationRateCategoryRepository>().Object,
            new Mock<ITenantDbContextFactory>().Object,
            NullLogger<AccountingService>.Instance,
            Options.Create(new AccountingSettings { ManualReversalEnabled = true }));

        var result = await BuildHandler(service)
            .Handle(new MassReverseEntriesCommand(new[] { sale.Id, purchase.Id }, "correction"), CancellationToken.None);

        Assert.Equal(2, result.Value.Reversed);
        Assert.Equal(2, reversals.Count);

        var movements = new[] { sale, purchase }.Concat(reversals)
            .SelectMany(e => e.Lines)
            .GroupBy(l => l.AccountNumber)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.DebitAmount.Amount - l.CreditAmount.Amount));

        Assert.Equal(4, movements.Count);                        // 4111, 707, 607, 4011
        Assert.All(movements, kv => Assert.Equal(0m, kv.Value));  // origine + extourne == 0
    }

    [Fact]
    public async Task RealService_FlagDisabled_ReversesNothing()
    {
        var validated = Entry(11, JournalEntryStatus.Validee);

        var journals = new Mock<IJournalEntryRepository>();
        journals.Setup(x => x.GetByIdAsync(validated.Id, It.IsAny<CancellationToken>())).ReturnsAsync(validated);

        var service = new AccountingService(
            new Mock<IChartOfAccountRepository>().Object, new Mock<IAccountingPeriodService>().Object,
            journals.Object, new Mock<IWithholdingTaxRepository>().Object,
            new Mock<IDepreciationRateCategoryRepository>().Object,
            new Mock<ITenantDbContextFactory>().Object, NullLogger<AccountingService>.Instance,
            Options.Create(new AccountingSettings { ManualReversalEnabled = false }));

        var result = await BuildHandler(service)
            .Handle(new MassReverseEntriesCommand(new[] { validated.Id }, "correction"), CancellationToken.None);

        Assert.Equal(0, result.Value.Reversed);
        Assert.Equal(1, result.Value.Skipped);
        Assert.False(validated.IsReversed);
    }
}
