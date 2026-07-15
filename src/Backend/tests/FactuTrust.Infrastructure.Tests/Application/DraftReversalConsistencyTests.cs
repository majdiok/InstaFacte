using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.Commands;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using FactuTrust.Infrastructure.MultiTenancy;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Repositories;
using FactuTrust.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

/// <summary>
/// C5 : cohérence extourne ↔ brouillon.
/// — Supprimer un brouillon d'extourne restaure l'écriture d'origine (dé-marquage IsReversed).
/// — Un brouillon d'extourne n'est pas modifiable (miroir exact garanti à la création).
/// — L'annulation d'une facture dont l'écriture est encore en brouillon supprime le brouillon
///   au lieu de générer une contre-passation.
/// </summary>
public sealed class DraftReversalConsistencyTests
{
    private static IReadOnlyList<JournalLineInput> SaleLines(decimal amount = 100m) => new[]
    {
        new JournalLineInput("4111", "Client", amount, 0, null, ThirdPartyKind.None),
        new JournalLineInput("707", "Ventes", 0, amount, null, ThirdPartyKind.None)
    };

    private static IReadOnlyList<JournalLineInput> MirrorLines(decimal amount = 100m) => new[]
    {
        new JournalLineInput("4111", "Client", 0, amount, null, ThirdPartyKind.None),
        new JournalLineInput("707", "Ventes", amount, 0, null, ThirdPartyKind.None)
    };

    // ── Suppression / modification d'un brouillon d'extourne (InMemory + vrai repository) ──

    private sealed class TestTenantDbContextFactory : ITenantDbContextFactory
    {
        private readonly string _databaseName;
        public TestTenantDbContextFactory(string databaseName) => _databaseName = databaseName;
        public TenantDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<TenantDbContext>()
                .UseInMemoryDatabase(databaseName: _databaseName)
                .Options;
            return new TenantDbContext(options);
        }
    }

    /// <summary>La période doit exister en base : GetByIdAsync fait un Include(AccountingPeriod) requis.</summary>
    private static AccountingPeriod SeedPeriod(TestTenantDbContextFactory factory)
    {
        var period = AccountingPeriod.Create(2026, 5, new DateTime(2026, 5, 1), new DateTime(2026, 5, 31));
        period.SetAuditInfo("test", false);
        using var ctx = factory.CreateContext();
        ctx.AccountingPeriods.Add(period);
        ctx.SaveChanges();
        return period;
    }

    private static (TestTenantDbContextFactory Factory, JournalEntry Original, JournalEntry ReversalDraft) SeedReversalPair()
    {
        var factory = new TestTenantDbContextFactory($"ReversalDb_{Guid.NewGuid()}");
        var period = SeedPeriod(factory);

        var original = JournalEntry.Create(1, "JV", new DateTime(2026, 5, 10), "Vente", period.Id,
            false, "Manual", null, SaleLines(), initialStatus: JournalEntryStatus.Validee).Value;
        original.SetAuditInfo("test", false);

        var reversal = JournalEntry.Create(2, "JV", new DateTime(2026, 5, 10), "Extourne JV-1 : erreur", period.Id,
            false, "ManualReversal", original.Id, MirrorLines(), Money.DefaultCurrency,
            reversesEntryId: original.Id, initialStatus: JournalEntryStatus.Brouillon).Value;
        reversal.SetAuditInfo("test", false);

        original.MarkReversedBy(reversal.Id);

        using var ctx = factory.CreateContext();
        ctx.JournalEntries.AddRange(original, reversal);
        ctx.SaveChanges();
        return (factory, original, reversal);
    }

    [Fact]
    public async Task DeleteReversalDraft_RestoresOriginal()
    {
        var (factory, original, reversal) = SeedReversalPair();
        var repo = new JournalEntryRepository(factory);
        var handler = new DeleteDraftJournalEntryCommandHandler(repo, new Mock<IAuditService>().Object);

        var result = await handler.Handle(new DeleteDraftJournalEntryCommand(reversal.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        using var ctx = factory.CreateContext();
        Assert.Null(await ctx.JournalEntries.AsNoTracking().FirstOrDefaultAsync(e => e.Id == reversal.Id));
        var restored = await ctx.JournalEntries.AsNoTracking().SingleAsync(e => e.Id == original.Id);
        Assert.False(restored.IsReversed);
        Assert.Null(restored.ReversedByEntryId);
    }

    [Fact]
    public async Task DeleteOrdinaryDraft_DoesNotTouchOtherEntries()
    {
        var factory = new TestTenantDbContextFactory($"ReversalDb_{Guid.NewGuid()}");
        var period = SeedPeriod(factory);
        var draft = JournalEntry.Create(1, "JOD", new DateTime(2026, 5, 10), "OD", period.Id,
            false, "Manual", null, SaleLines(), initialStatus: JournalEntryStatus.Brouillon).Value;
        draft.SetAuditInfo("test", false);
        using (var seed = factory.CreateContext())
        {
            seed.JournalEntries.Add(draft);
            seed.SaveChanges();
        }
        var handler = new DeleteDraftJournalEntryCommandHandler(new JournalEntryRepository(factory), new Mock<IAuditService>().Object);

        var result = await handler.Handle(new DeleteDraftJournalEntryCommand(draft.Id), CancellationToken.None);

        Assert.True(result.IsSuccess);
        using var ctx = factory.CreateContext();
        Assert.Empty(ctx.JournalEntries.AsNoTracking().ToList());
    }

    [Fact]
    public async Task UpdateReversalDraft_Fails()
    {
        var (factory, _, reversal) = SeedReversalPair();
        var repo = new JournalEntryRepository(factory);
        var chart = new Mock<IChartOfAccountRepository>();
        chart.Setup(x => x.GetByAccountNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string acc, CancellationToken _) =>
                ChartOfAccount.Create(acc, $"Compte {acc}", int.Parse(acc[..1]), null, AccountNatureType.Debit).Value);
        var handler = new UpdateDraftJournalEntryCommandHandler(repo, chart.Object, new Mock<IAuditService>().Object);

        var request = new UpdateDraftJournalEntryRequest
        {
            Label = "Extourne modifiée",
            Lines = new[]
            {
                new ManualJournalLineRequest { AccountNumber = "4111", LineLabel = "Client", Debit = 0m, Credit = 999m },
                new ManualJournalLineRequest { AccountNumber = "707", LineLabel = "Vente", Debit = 999m, Credit = 0m }
            }
        };

        var result = await handler.Handle(new UpdateDraftJournalEntryCommand(reversal.Id, request), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("miroir", result.Error.Description);
    }

    // ── Annulation de facture avec écriture d'origine en brouillon (Moq pur) ───────────────

    [Fact]
    public async Task CancelInvoice_WithDraftOriginal_DeletesDraftInsteadOfReversing()
    {
        var draftOriginal = JournalEntry.Create(1, "JV", new DateTime(2026, 5, 10), "Vente FAC-1", Guid.NewGuid(),
            true, "Invoice", Guid.NewGuid(), SaleLines(), initialStatus: JournalEntryStatus.Brouillon).Value;

        var chart = new Mock<IChartOfAccountRepository>();
        chart.Setup(x => x.CountAsync(It.IsAny<CancellationToken>())).ReturnsAsync(180);

        var journals = new Mock<IJournalEntryRepository>();
        journals.Setup(x => x.GetBySourceAsync("InvoiceCancelled", It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JournalEntry?)null);
        journals.Setup(x => x.GetBySourceAsync("Invoice", It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(draftOriginal);

        var periodService = new Mock<IAccountingPeriodService>();
        var withholding = new Mock<IWithholdingTaxRepository>();
        var ctxFactory = new Mock<ITenantDbContextFactory>();
        var settings = Options.Create(new AccountingSettings { BrouillardEnabled = true });

        var service = new AccountingService(
            chart.Object, periodService.Object, journals.Object, withholding.Object,
            ctxFactory.Object, NullLogger<AccountingService>.Instance, settings);

        var result = await service.ReverseInvoiceSaleEntryAsync(Guid.NewGuid(), "FAC-1", CancellationToken.None);

        Assert.True(result.IsSuccess);
        journals.Verify(x => x.RemoveAsync(draftOriginal, It.IsAny<CancellationToken>()), Times.Once);
        journals.Verify(x => x.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.False(draftOriginal.IsReversed);
    }
}
