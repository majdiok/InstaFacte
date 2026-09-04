using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services.RecurringContracts;

/// <summary>Factures liées (T8/D5) : union factures + avoirs, tri date desc, périodes de run.</summary>
public sealed class RecurringContractLinkedInvoicesTests
{
    [Fact]
    public async Task LinkedInvoices_ReturnsInvoicesOrderedByDateDesc_WithRunPeriods()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client factures liées");
        var contract = await harness.SeedContractAsync(client.Id,
            startDate: new DateTime(2026, 1, 1),
            configure: c => c.Activate(),
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m, RecurringContractTestHarness.DefaultProductId));

        var invoice1 = await harness.SeedInvoiceAsync(client.Id, new DateTime(2026, 1, 15), 100m,
            dueDate: new DateTime(2026, 2, 14));
        var run1 = await harness.SeedRunAsync(contract.Id, new DateTime(2026, 1, 1), new DateTime(2026, 1, 31),
            RecurringContractBillingRunStatus.Invoiced, fixedAmount: 100m, invoiceId: invoice1.Id);
        await harness.LinkInvoiceToContractAsync(invoice1.Id, contract.Id, run1.Id);

        var invoice2 = await harness.SeedInvoiceAsync(client.Id, new DateTime(2026, 2, 15), 200m);
        var run2 = await harness.SeedRunAsync(contract.Id, new DateTime(2026, 2, 1), new DateTime(2026, 2, 28),
            RecurringContractBillingRunStatus.Invoiced, fixedAmount: 200m, invoiceId: invoice2.Id);
        await harness.LinkInvoiceToContractAsync(invoice2.Id, contract.Id, run2.Id);

        await using var sut = harness.CreateService();
        var items = await sut.GetLinkedInvoicesAsync(contract.Id);

        Assert.NotNull(items);
        Assert.Equal(2, items!.Count);

        // Plus récente d'abord.
        Assert.Equal(invoice2.Id, items[0].InvoiceId);
        Assert.Equal(invoice1.Id, items[1].InvoiceId);

        var second = items[0];
        Assert.Equal(run2.Id, second.BillingRunId);
        Assert.Equal(new DateTime(2026, 2, 1), second.PeriodFrom);
        Assert.Equal(new DateTime(2026, 2, 28), second.PeriodTo);
        Assert.Equal(200m, second.AmountHT);
        Assert.Equal(200m * 1.19m, second.AmountTTC);
        Assert.False(second.IsCreditNote);
        Assert.Equal(InvoiceStatus.Validated, second.Status);
        Assert.Equal("Validée", second.StatusDisplay);

        var first = items[1];
        Assert.Equal(run1.Id, first.BillingRunId);
        Assert.Equal(new DateTime(2026, 1, 1), first.PeriodFrom);
        Assert.Equal(new DateTime(2026, 1, 31), first.PeriodTo);
        Assert.Equal(new DateTime(2026, 2, 14), first.DueDate);
        Assert.Equal(100m, first.AmountHT);
    }

    [Fact]
    public async Task LinkedInvoices_IncludesCreditNotesLinkedToContractInvoices()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client avoirs");
        var contract = await harness.SeedContractAsync(client.Id,
            startDate: new DateTime(2026, 1, 1),
            configure: c => c.Activate(),
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m, RecurringContractTestHarness.DefaultProductId));

        var invoice = await harness.SeedInvoiceAsync(client.Id, new DateTime(2026, 2, 1), 250m);
        var run = await harness.SeedRunAsync(contract.Id, new DateTime(2026, 2, 1), new DateTime(2026, 2, 28),
            RecurringContractBillingRunStatus.Invoiced, fixedAmount: 250m, invoiceId: invoice.Id);
        await harness.LinkInvoiceToContractAsync(invoice.Id, contract.Id, run.Id);
        // L'avoir ne porte PAS le lien contrat : il est découvert via LinkedInvoiceId.
        var note = await harness.SeedCreditNoteAsync(client.Id, invoice.Id, new DateTime(2026, 2, 20), 50m);

        await using var sut = harness.CreateService();
        var items = await sut.GetLinkedInvoicesAsync(contract.Id);

        Assert.NotNull(items);
        Assert.Equal(2, items!.Count);

        var creditNote = Assert.Single(items, i => i.IsCreditNote);
        Assert.Equal(note.Id, creditNote.InvoiceId);
        Assert.Equal(-50m, creditNote.AmountHT);                       // montants signés négatifs
        Assert.Equal(-50m * 1.19m, creditNote.AmountTTC);
        // Run et périodes résolus via le document d'origine.
        Assert.Equal(run.Id, creditNote.BillingRunId);
        Assert.Equal(new DateTime(2026, 2, 1), creditNote.PeriodFrom);
        Assert.Equal(new DateTime(2026, 2, 28), creditNote.PeriodTo);
    }

    [Fact]
    public async Task LinkedInvoices_DoesNotDuplicateCreditNote_WhenItAlreadyCarriesContractLink()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client avoir lié");
        var contract = await harness.SeedContractAsync(client.Id,
            startDate: new DateTime(2026, 1, 1),
            configure: c => c.Activate(),
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m, RecurringContractTestHarness.DefaultProductId));

        var invoice = await harness.SeedInvoiceAsync(client.Id, new DateTime(2026, 2, 1), 250m);
        var run = await harness.SeedRunAsync(contract.Id, new DateTime(2026, 2, 1), new DateTime(2026, 2, 28),
            RecurringContractBillingRunStatus.Invoiced, fixedAmount: 250m, invoiceId: invoice.Id);
        await harness.LinkInvoiceToContractAsync(invoice.Id, contract.Id, run.Id);
        var note = await harness.SeedCreditNoteAsync(client.Id, invoice.Id, new DateTime(2026, 2, 20), 50m);
        // L'avoir porte LUI-AUSSI le lien contrat → il ne doit apparaître qu'une fois.
        await harness.LinkInvoiceToContractAsync(note.Id, contract.Id, run.Id);

        await using var sut = harness.CreateService();
        var items = await sut.GetLinkedInvoicesAsync(contract.Id);

        Assert.NotNull(items);
        Assert.Equal(2, items!.Count);
        Assert.Single(items, i => i.IsCreditNote);
        Assert.Single(items, i => !i.IsCreditNote);
    }

    [Fact]
    public async Task LinkedInvoices_UnknownContract_ReturnsNull()
    {
        var harness = new RecurringContractTestHarness();
        await using var sut = harness.CreateService();

        var items = await sut.GetLinkedInvoicesAsync(Guid.NewGuid());

        Assert.Null(items);
    }

    [Fact]
    public async Task LinkedInvoices_NoInvoices_ReturnsEmpty()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client sans factures");
        var contract = await harness.SeedContractAsync(client.Id,
            startDate: new DateTime(2026, 1, 1),
            configure: c => c.Activate(),
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m, RecurringContractTestHarness.DefaultProductId));

        await using var sut = harness.CreateService();
        var items = await sut.GetLinkedInvoicesAsync(contract.Id);

        Assert.NotNull(items);
        Assert.Empty(items!);
    }
}
