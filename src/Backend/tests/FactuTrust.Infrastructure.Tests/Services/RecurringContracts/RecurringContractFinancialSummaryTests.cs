using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services.RecurringContracts;

/// <summary>Résumé financier (T7/D4) : fenêtre contractuelle, total estimé, HT net facturé.</summary>
public sealed class RecurringContractFinancialSummaryTests
{
    private static readonly DateTime Today = DateTime.UtcNow.Date;

    [Fact]
    public async Task Summary_WithEndDate_TotalEqualsSumOfWindowOccurrences()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client summary");
        var contract = await harness.SeedContractAsync(client.Id,
            startDate: new DateTime(2026, 1, 1), endDate: new DateTime(2026, 12, 31),
            configure: c => c.Activate(),
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m));

        await using var sut = harness.CreateService();
        var summary = await sut.GetFinancialSummaryAsync(contract.Id);

        Assert.NotNull(summary);
        Assert.Equal(contract.Id, summary!.ContractId);
        Assert.Equal(new DateTime(2026, 1, 1), summary.WindowFrom);
        Assert.Equal(new DateTime(2026, 12, 31), summary.WindowTo);
        Assert.False(summary.IsOpenEnded);
        // 12 occurrences mensuelles × 100 HT (plein tarif sur chaque période).
        Assert.Equal(12, summary.TotalRunsCount);
        Assert.Equal(1200m, summary.TotalContractAmount);
        Assert.Equal(0m, summary.TotalInvoicedAmount);
        Assert.Equal(1200m, summary.RemainingAmount);
        Assert.Equal(0m, summary.PercentInvoiced);
        Assert.Equal(0, summary.InvoicedRunsCount);
        Assert.Equal("TND", summary.Currency);
    }

    [Fact]
    public async Task Summary_MidLifeContract_TotalCoversFullWindow()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client mi-vie");
        var contract = await harness.SeedContractAsync(client.Id,
            startDate: new DateTime(2026, 1, 1), endDate: new DateTime(2026, 12, 31),
            configure: c =>
            {
                c.Activate();
                // Contrat « à mi-vie » : 6 périodes déjà facturées, NextBillingDate = 01/07/2026.
                for (var i = 0; i < 6; i++) c.AdvanceBillingSchedule();
            },
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m));

        await using var sut = harness.CreateService();
        var summary = await sut.GetFinancialSummaryAsync(contract.Id);

        // Verrouille la règle D4 : la projection part de la date initiale (jamais de
        // NextBillingDate) — le total inclut les périodes passées de la fenêtre.
        Assert.NotNull(summary);
        Assert.Equal(12, summary!.TotalRunsCount);
        Assert.Equal(1200m, summary.TotalContractAmount);
    }

    [Fact]
    public async Task Summary_OpenEnded_UsesRolling12MonthWindow_AndIsOpenEndedTrue()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client sans fin");
        var contract = await harness.SeedContractAsync(client.Id,
            startDate: new DateTime(2026, 1, 1), endDate: null,
            configure: c => c.Activate(),
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m));

        await using var sut = harness.CreateService();
        var summary = await sut.GetFinancialSummaryAsync(contract.Id);

        Assert.NotNull(summary);
        Assert.True(summary!.IsOpenEnded);
        // Fenêtre glissante : [aujourd'hui, +12 mois − 1 jour] car StartDate est passée.
        Assert.Equal(Today, summary.WindowFrom);
        Assert.Equal(Today.AddMonths(12).AddDays(-1), summary.WindowTo);
        // Toujours exactement 12 premiers du mois dans une fenêtre glissante de 12 mois.
        Assert.Equal(12, summary.TotalRunsCount);
        Assert.Equal(1200m, summary.TotalContractAmount);
    }

    [Fact]
    public async Task Summary_InvoicedAmount_ExcludesDraftCreatedRuns()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client brouillon");
        var contract = await harness.SeedContractAsync(client.Id,
            startDate: new DateTime(2026, 1, 1), endDate: new DateTime(2026, 12, 31),
            configure: c => c.Activate(),
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m));
        // Run DraftCreated : un brouillon existe mais rien n'est réellement facturé.
        await harness.SeedRunAsync(contract.Id, new DateTime(2026, 1, 1), new DateTime(2026, 1, 31),
            RecurringContractBillingRunStatus.DraftCreated, fixedAmount: 100m);

        await using var sut = harness.CreateService();
        var summary = await sut.GetFinancialSummaryAsync(contract.Id);

        Assert.NotNull(summary);
        Assert.Equal(0m, summary!.TotalInvoicedAmount);
        Assert.Equal(0, summary.InvoicedRunsCount);
        Assert.Equal(1200m, summary.TotalContractAmount);
        Assert.Equal(1200m, summary.RemainingAmount);
    }

    [Fact]
    public async Task Summary_InvoicedAmount_UsesInvoiceSubTotal_WhenInvoiceExists()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client facturé");
        var contract = await harness.SeedContractAsync(client.Id,
            startDate: new DateTime(2026, 1, 1), endDate: new DateTime(2026, 12, 31),
            configure: c => c.Activate(),
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m));
        var invoice = await harness.SeedInvoiceAsync(client.Id, new DateTime(2026, 2, 1), 250m);
        // Jointure couverte via run.InvoiceId uniquement (la facture ne porte pas le lien contrat).
        await harness.SeedRunAsync(contract.Id, new DateTime(2026, 1, 1), new DateTime(2026, 1, 31),
            RecurringContractBillingRunStatus.Invoiced, fixedAmount: 100m, invoiceId: invoice.Id);

        await using var sut = harness.CreateService();
        var summary = await sut.GetFinancialSummaryAsync(contract.Id);

        Assert.NotNull(summary);
        // Le SubTotal réel de la facture (250) prime sur l'estimation du run (100).
        Assert.Equal(250m, summary!.TotalInvoicedAmount);
        Assert.Equal(1, summary.InvoicedRunsCount);
        Assert.Equal(950m, summary.RemainingAmount);
        Assert.Equal(decimal.Round(250m / 1200m * 100m, 2, MidpointRounding.AwayFromZero),
            summary.PercentInvoiced);
    }

    [Fact]
    public async Task Summary_CancelledInvoice_CountsZero_AndExcludedFromInvoicedRunsCount()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client annulé");
        var contract = await harness.SeedContractAsync(client.Id,
            startDate: new DateTime(2026, 1, 1), endDate: new DateTime(2026, 12, 31),
            configure: c => c.Activate(),
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m));
        var invoice = await harness.SeedInvoiceAsync(client.Id, new DateTime(2026, 2, 1), 100m,
            cancel: true);
        // Jointure couverte via invoice.SourceRecurringContractBillingRunId (autre clé de liaison).
        var run = await harness.SeedRunAsync(contract.Id, new DateTime(2026, 1, 1), new DateTime(2026, 1, 31),
            RecurringContractBillingRunStatus.Invoiced, fixedAmount: 100m);
        await harness.LinkInvoiceToContractAsync(invoice.Id, contract.Id, run.Id);

        await using var sut = harness.CreateService();
        var summary = await sut.GetFinancialSummaryAsync(contract.Id);

        Assert.NotNull(summary);
        Assert.Equal(0m, summary!.TotalInvoicedAmount);
        Assert.Equal(0, summary.InvoicedRunsCount);
        Assert.Equal(1200m, summary.RemainingAmount);
    }

    [Fact]
    public async Task Summary_CreditNote_ReducesInvoicedAmount()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client avoir");
        var contract = await harness.SeedContractAsync(client.Id,
            startDate: new DateTime(2026, 1, 1), endDate: new DateTime(2026, 12, 31),
            configure: c => c.Activate(),
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m));
        var invoice = await harness.SeedInvoiceAsync(client.Id, new DateTime(2026, 2, 1), 250m);
        var run = await harness.SeedRunAsync(contract.Id, new DateTime(2026, 1, 1), new DateTime(2026, 1, 31),
            RecurringContractBillingRunStatus.Invoiced, fixedAmount: 250m, invoiceId: invoice.Id);
        await harness.LinkInvoiceToContractAsync(invoice.Id, contract.Id, run.Id);
        // Avoir validé de 50 HT rattaché via LinkedInvoiceId (SubTotal signé −50).
        await harness.SeedCreditNoteAsync(client.Id, invoice.Id, new DateTime(2026, 2, 10), 50m);

        await using var sut = harness.CreateService();
        var summary = await sut.GetFinancialSummaryAsync(contract.Id);

        Assert.NotNull(summary);
        Assert.Equal(200m, summary!.TotalInvoicedAmount);
        Assert.Equal(1, summary.InvoicedRunsCount);
    }

    [Fact]
    public async Task Summary_CancelledCreditNote_DoesNotReduce()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client avoir annulé");
        var contract = await harness.SeedContractAsync(client.Id,
            startDate: new DateTime(2026, 1, 1), endDate: new DateTime(2026, 12, 31),
            configure: c => c.Activate(),
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m));
        var invoice = await harness.SeedInvoiceAsync(client.Id, new DateTime(2026, 2, 1), 250m);
        var run = await harness.SeedRunAsync(contract.Id, new DateTime(2026, 1, 1), new DateTime(2026, 1, 31),
            RecurringContractBillingRunStatus.Invoiced, fixedAmount: 250m, invoiceId: invoice.Id);
        await harness.LinkInvoiceToContractAsync(invoice.Id, contract.Id, run.Id);
        // Avoir annulé → exclu des agrégats.
        await harness.SeedCreditNoteAsync(client.Id, invoice.Id, new DateTime(2026, 2, 10), 50m,
            cancel: true);

        await using var sut = harness.CreateService();
        var summary = await sut.GetFinancialSummaryAsync(contract.Id);

        Assert.NotNull(summary);
        Assert.Equal(250m, summary!.TotalInvoicedAmount);
        Assert.Equal(1, summary.InvoicedRunsCount);
    }

    [Fact]
    public async Task Summary_PercentInvoiced_ZeroWhenTotalZero()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client sans lignes");
        var contract = await harness.SeedContractAsync(client.Id,
            startDate: new DateTime(2026, 1, 1), endDate: new DateTime(2026, 12, 31));

        await using var sut = harness.CreateService();
        var summary = await sut.GetFinancialSummaryAsync(contract.Id);

        Assert.NotNull(summary);
        Assert.Equal(0m, summary!.TotalContractAmount);
        Assert.Equal(0m, summary.TotalInvoicedAmount);
        Assert.Equal(0m, summary.PercentInvoiced);
        Assert.Equal(0m, summary.RemainingAmount);
    }

    [Fact]
    public async Task Summary_UnknownContract_ReturnsNull()
    {
        var harness = new RecurringContractTestHarness();
        await using var sut = harness.CreateService();

        var summary = await sut.GetFinancialSummaryAsync(Guid.NewGuid());

        Assert.Null(summary);
    }
}
