using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
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

namespace FactuTrust.Infrastructure.Tests.Services;

public sealed class AccountingServiceReversalTests
{
    private static IReadOnlyList<JournalLineInput> SaleLines() => new[]
    {
        new JournalLineInput("4111", "Client", 100m, 0, null, ThirdPartyKind.None),
        new JournalLineInput("707", "Ventes", 0, 100m, null, ThirdPartyKind.None)
    };

    private static JournalEntry ValidatedEntry(JournalEntryStatus status = JournalEntryStatus.Validee) =>
        JournalEntry.Create(10, "JV", new DateTime(2026, 4, 15), "Vente", Guid.NewGuid(),
            false, "Invoice", Guid.NewGuid(), SaleLines(), Money.DefaultCurrency, null, status).Value;

    private static (AccountingService service, Mock<IJournalEntryRepository> journals) BuildService(
        bool manualReversalEnabled,
        JournalEntry? original)
    {
        var chart = new Mock<IChartOfAccountRepository>();
        var periodService = new Mock<IAccountingPeriodService>();
        var journals = new Mock<IJournalEntryRepository>();
        var withholding = new Mock<IWithholdingTaxRepository>();
        var ctxFactory = new Mock<ITenantDbContextFactory>();
        var settings = Options.Create(new AccountingSettings { ManualReversalEnabled = manualReversalEnabled });

        if (original is not null)
        {
            journals.Setup(x => x.GetByIdAsync(original.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(original);
        }

        var period = AccountingPeriod.Create(2026, 4, new DateTime(2026, 4, 1), new DateTime(2026, 4, 30));
        periodService.Setup(x => x.EnsureOpenPeriodAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(period));
        journals.Setup(x => x.ReserveNextEntryNumberAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(11);
        journals.Setup(x => x.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JournalEntry e, CancellationToken _) => e);

        var service = new AccountingService(
            chart.Object, periodService.Object, journals.Object, withholding.Object,
            ctxFactory.Object, NullLogger<AccountingService>.Instance, settings);

        return (service, journals);
    }

    [Fact]
    public async Task ReverseJournalEntry_WhenFlagDisabled_Fails()
    {
        var (service, _) = BuildService(manualReversalEnabled: false, original: ValidatedEntry());

        var result = await service.ReverseJournalEntryAsync(Guid.NewGuid(), "erreur", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("activée", result.Error.Description);
    }

    [Fact]
    public async Task ReverseJournalEntry_WhenDraft_Fails()
    {
        var draft = ValidatedEntry(JournalEntryStatus.Brouillon);
        var (service, journals) = BuildService(manualReversalEnabled: true, original: draft);

        var result = await service.ReverseJournalEntryAsync(draft.Id, "erreur", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("brouillon", result.Error.Description);
        journals.Verify(x => x.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReverseJournalEntry_WhenAlreadyReversed_Fails()
    {
        var entry = ValidatedEntry();
        entry.MarkReversedBy(Guid.NewGuid());
        var (service, journals) = BuildService(manualReversalEnabled: true, original: entry);

        var result = await service.ReverseJournalEntryAsync(entry.Id, "erreur", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("déjà", result.Error.Description);
        journals.Verify(x => x.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ReverseJournalEntry_HappyPath_SwapsDebitCreditAndMarksReversed()
    {
        var original = ValidatedEntry();
        JournalEntry? captured = null;
        var (service, journals) = BuildService(manualReversalEnabled: true, original: original);
        journals.Setup(x => x.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()))
            .Callback<JournalEntry, CancellationToken>((e, _) => captured = e)
            .ReturnsAsync((JournalEntry e, CancellationToken _) => e);

        var result = await service.ReverseJournalEntryAsync(original.Id, "erreur d'imputation", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(captured);
        Assert.Equal("JV", captured!.JournalCode);
        Assert.Equal(original.Id, captured.ReversesEntryId);
        Assert.Equal(AccountingService.SourceManualReversal, captured.SourceEntityType);

        // La ligne d'origine (débit 100 sur 4111) devient un crédit 100 sur 4111 dans la contre-passation.
        var revFirst = captured.Lines.OrderBy(l => l.LineNumber).First();
        Assert.Equal("4111", revFirst.AccountNumber);
        Assert.Equal(0m, revFirst.DebitAmount.Amount);
        Assert.Equal(100m, revFirst.CreditAmount.Amount);

        Assert.True(original.IsReversed);
        Assert.Equal(captured.Id, original.ReversedByEntryId);
        journals.Verify(x => x.UpdateAsync(original, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReverseJournalEntry_WhenBrouillardEnabled_ReversalIsDraft()
    {
        var original = ValidatedEntry();
        JournalEntry? captured = null;
        var chart = new Mock<IChartOfAccountRepository>();
        var periodService = new Mock<IAccountingPeriodService>();
        var journals = new Mock<IJournalEntryRepository>();
        var withholding = new Mock<IWithholdingTaxRepository>();
        var ctxFactory = new Mock<ITenantDbContextFactory>();
        var settings = Options.Create(new AccountingSettings { ManualReversalEnabled = true, BrouillardEnabled = true });

        journals.Setup(x => x.GetByIdAsync(original.Id, It.IsAny<CancellationToken>())).ReturnsAsync(original);
        var period = AccountingPeriod.Create(2026, 4, new DateTime(2026, 4, 1), new DateTime(2026, 4, 30));
        periodService.Setup(x => x.EnsureOpenPeriodAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(period));
        journals.Setup(x => x.ReserveNextEntryNumberAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(11);
        journals.Setup(x => x.AddAsync(It.IsAny<JournalEntry>(), It.IsAny<CancellationToken>()))
            .Callback<JournalEntry, CancellationToken>((e, _) => captured = e)
            .ReturnsAsync((JournalEntry e, CancellationToken _) => e);

        var service = new AccountingService(
            chart.Object, periodService.Object, journals.Object, withholding.Object,
            ctxFactory.Object, NullLogger<AccountingService>.Instance, settings);

        var result = await service.ReverseJournalEntryAsync(original.Id, "erreur", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(captured);
        Assert.Equal(JournalEntryStatus.Brouillon, captured!.Status);
    }
}
