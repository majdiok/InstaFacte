using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.InvoiceWizard.Commands;
using FactuTrust.Application.Features.RecurringContracts;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Entities.RecurringContracts;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services.RecurringContracts;

public sealed class RecurringContractAdjustDraftTests
{
    private static DraftInvoiceLine CatalogLine(
        string productId,
        string designation = "Abonnement",
        decimal qty = 1m,
        decimal price = 100m,
        int vat = 19) => new()
    {
        ProductId = productId,
        Designation = designation,
        Quantity = qty,
        UnitPriceHT = price,
        VatRate = vat,
        PriceOverridden = true
    };

    private static DraftInvoiceLine CustomLine(
        string designation,
        decimal qty = 1m,
        decimal price = 50m,
        int vat = 19) => new()
    {
        Designation = designation,
        Quantity = qty,
        UnitPriceHT = price,
        VatRate = vat,
        PriceOverridden = true
    };

    private static AdjustRecurringDraftLinesRequest Request(params AdjustRecurringDraftLineDto[] lines) =>
        new() { Lines = lines };

    private static AdjustRecurringDraftLineDto Patch(int index, string designation, decimal qty, decimal price) =>
        new() { Index = index, Designation = designation, Quantity = qty, UnitPriceHT = price };

    [Fact]
    public async Task Adjust_UpdatesAllowedFields_KeepsDraftAndImmutableFields()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client ajustement");
        var contract = await harness.SeedContractAsync(client.Id,
            configure: c => c.Activate(),
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m));
        var productId = Guid.NewGuid().ToString();
        var draft = await harness.SeedInvoiceDraftAsync(
        [
            CatalogLine(productId, "Abonnement", 1m, 100m, 19),
            CustomLine("Consommation période", 1m, 20m, 19)
        ]);
        var run = await harness.SeedRunAsync(
            contract.Id, new DateTime(2026, 1, 1), new DateTime(2026, 1, 31),
            RecurringContractBillingRunStatus.DraftCreated, fixedAmount: 100m, invoiceDraftId: draft.Id);

        await using var sut = harness.CreateService();
        var result = await sut.AdjustDraftLinesAsync(run.Id, Request(
            Patch(0, "Abonnement ajusté", 2m, 150m),
            Patch(1, "Consommation ajustée", 1m, 35m)));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Lines.Count);
        Assert.Equal("Abonnement ajusté", result.Value.Lines[0].Designation);
        Assert.Equal(2m, result.Value.Lines[0].Quantity);
        Assert.Equal(150m, result.Value.Lines[0].UnitPriceHT);
        Assert.Equal(productId, result.Value.Lines[0].ProductId);
        Assert.Equal(19, result.Value.Lines[0].VatRate);
        Assert.Equal("Consommation ajustée", result.Value.Lines[1].Designation);
        Assert.Equal(35m, result.Value.Lines[1].UnitPriceHT);
        Assert.Null(result.Value.Lines[1].ProductId);

        await using var ctx = harness.Factory.CreateContext();
        var persistedRun = await ctx.RecurringContractBillingRuns.FindAsync(run.Id);
        var persistedDraft = await ctx.InvoiceDrafts.FindAsync(draft.Id);
        Assert.Equal(RecurringContractBillingRunStatus.DraftCreated, persistedRun!.Status);
        Assert.Null(persistedRun.InvoiceId);
        Assert.False(persistedDraft!.IsConverted);
        var lines = persistedDraft.GetLines();
        Assert.Equal(2, lines.Count);
        Assert.Equal(productId, lines[0].ProductId);
        Assert.Equal(19, lines[0].VatRate);
        Assert.True(lines[0].PriceOverridden);
        Assert.Equal(19, lines[1].VatRate);
    }

    [Fact]
    public async Task Adjust_IgnoresAttemptToChangeLineCount()
    {
        var harness = new RecurringContractTestHarness();
        var (run, _) = await SeedDraftCreatedAsync(harness, [CustomLine("Ligne unique")]);

        await using var sut = harness.CreateService();
        var result = await sut.AdjustDraftLinesAsync(run.Id, Request(
            Patch(0, "A", 1m, 10m),
            Patch(1, "B", 1m, 10m)));

        Assert.True(result.IsFailure);
        Assert.Contains("nombre de lignes", result.Error.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Adjust_RejectsMissingOrOutOfRangeIndex()
    {
        var harness = new RecurringContractTestHarness();
        var (run, _) = await SeedDraftCreatedAsync(harness,
        [
            CustomLine("L1"),
            CustomLine("L2")
        ]);

        await using var sut = harness.CreateService();
        var result = await sut.AdjustDraftLinesAsync(run.Id, Request(
            Patch(0, "L1", 1m, 10m),
            Patch(2, "L2", 1m, 10m)));

        Assert.True(result.IsFailure);
        Assert.Contains("index", result.Error.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Adjust_RejectsNonPositiveQuantity()
    {
        var harness = new RecurringContractTestHarness();
        var (run, _) = await SeedDraftCreatedAsync(harness, [CustomLine("Ligne")]);

        await using var sut = harness.CreateService();
        var result = await sut.AdjustDraftLinesAsync(run.Id, Request(Patch(0, "Ligne", 0m, 10m)));

        Assert.True(result.IsFailure);
        Assert.Contains("quantité", result.Error.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Adjust_RejectsNegativeUnitPrice()
    {
        var harness = new RecurringContractTestHarness();
        var (run, _) = await SeedDraftCreatedAsync(harness, [CustomLine("Ligne")]);

        await using var sut = harness.CreateService();
        var result = await sut.AdjustDraftLinesAsync(run.Id, Request(Patch(0, "Ligne", 1m, -5m)));

        Assert.True(result.IsFailure);
        Assert.Contains("prix", result.Error.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Adjust_InvoicedRun_DoesNotMutateDraft()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client invoiced");
        var contract = await harness.SeedContractAsync(client.Id, configure: c => c.Activate());
        var draft = await harness.SeedInvoiceDraftAsync([CustomLine("Originale", 1m, 80m)]);
        var run = await harness.SeedRunAsync(
            contract.Id, new DateTime(2026, 3, 1), new DateTime(2026, 3, 31),
            RecurringContractBillingRunStatus.Invoiced, invoiceDraftId: draft.Id, invoiceId: Guid.NewGuid());

        await using var sut = harness.CreateService();
        var result = await sut.AdjustDraftLinesAsync(run.Id, Request(Patch(0, "Changée", 9m, 1m)));

        Assert.True(result.IsFailure);
        Assert.Equal("Conflict", result.Error.Code);

        await using var ctx = harness.Factory.CreateContext();
        var persisted = (await ctx.InvoiceDrafts.FindAsync(draft.Id))!.GetLines();
        Assert.Equal("Originale", persisted[0].Designation);
        Assert.Equal(80m, persisted[0].UnitPriceHT);
    }

    [Fact]
    public async Task Adjust_PendingRun_DoesNotSubmitOrAdjust()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client pending");
        var contract = await harness.SeedContractAsync(client.Id, configure: c => c.Activate());
        var run = await harness.SeedRunAsync(
            contract.Id, new DateTime(2026, 2, 1), new DateTime(2026, 2, 28),
            RecurringContractBillingRunStatus.Pending);

        await using var sut = harness.CreateService();
        var result = await sut.AdjustDraftLinesAsync(run.Id, Request(Patch(0, "X", 1m, 1m)));

        Assert.True(result.IsFailure);
        Assert.Equal("Conflict", result.Error.Code);
    }

    [Fact]
    public async Task Adjust_ConvertedDraft_ReturnsConflict()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client converted");
        var contract = await harness.SeedContractAsync(client.Id, configure: c => c.Activate());
        var draft = await harness.SeedInvoiceDraftAsync([CustomLine("Ligne")], converted: true);
        var run = await harness.SeedRunAsync(
            contract.Id, new DateTime(2026, 4, 1), new DateTime(2026, 4, 30),
            RecurringContractBillingRunStatus.DraftCreated, invoiceDraftId: draft.Id);

        await using var sut = harness.CreateService();
        var result = await sut.AdjustDraftLinesAsync(run.Id, Request(Patch(0, "Ligne", 1m, 10m)));

        Assert.True(result.IsFailure);
        Assert.Equal("Conflict", result.Error.Code);
    }

    [Fact]
    public async Task Adjust_ExpiredDraft_ReturnsConflict()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client expired");
        var contract = await harness.SeedContractAsync(client.Id, configure: c => c.Activate());
        var draft = await harness.SeedInvoiceDraftAsync([CustomLine("Ligne")], expired: true);
        var run = await harness.SeedRunAsync(
            contract.Id, new DateTime(2026, 5, 1), new DateTime(2026, 5, 31),
            RecurringContractBillingRunStatus.DraftCreated, invoiceDraftId: draft.Id);

        await using var sut = harness.CreateService();
        var result = await sut.AdjustDraftLinesAsync(run.Id, Request(Patch(0, "Ligne", 1m, 10m)));

        Assert.True(result.IsFailure);
        Assert.Equal("Conflict", result.Error.Code);
    }

    [Fact]
    public async Task Get_UnknownRun_ReturnsNotFound()
    {
        var harness = new RecurringContractTestHarness();
        await using var sut = harness.CreateService();
        var result = await sut.GetAdjustableDraftAsync(Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Contains("NotFound", result.Error.Code, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_DraftCreated_ReturnsLinesAndTotals()
    {
        var harness = new RecurringContractTestHarness();
        var (run, draft) = await SeedDraftCreatedAsync(harness, [CustomLine("Prestation", 2m, 50m, 19)]);

        await using var sut = harness.CreateService();
        var result = await sut.GetAdjustableDraftAsync(run.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(draft.Id, result.Value.InvoiceDraftId);
        Assert.Equal(run.Id, result.Value.BillingRunId);
        Assert.False(result.Value.IsConverted);
        var line = Assert.Single(result.Value.Lines);
        Assert.Equal(0, line.Index);
        Assert.Equal("Prestation", line.Designation);
        Assert.Equal(2m, line.Quantity);
        Assert.Equal(50m, line.UnitPriceHT);
        Assert.Equal(19, line.VatRate);
        Assert.Equal(100m, result.Value.Totals.TotalHT);
        Assert.Equal(19m, result.Value.Totals.TotalVat);
    }

    [Fact]
    public async Task Adjust_ThenIssue_SendsExistingSubmitInvoiceCommandWithStableKey()
    {
        var harness = new RecurringContractTestHarness();
        var (run, draft) = await SeedDraftCreatedAsync(harness, [CustomLine("Abonnement", 1m, 100m)]);

        await using var adjust = harness.CreateService();
        var adjusted = await adjust.AdjustDraftLinesAsync(run.Id, Request(Patch(0, "Abonnement", 3m, 80m)));
        Assert.True(adjusted.IsSuccess);

        var invoiceId = Guid.NewGuid();
        var mediator = new RecordingMediator
        {
            OnSend = request => request is SubmitInvoiceCommand
                ? Result.Success(new InvoiceCreatedResultDto
                {
                    InvoiceId = invoiceId,
                    InvoiceNumber = "FAC-2026-0100",
                    Status = "Validée",
                    CreatedAt = DateTime.UtcNow
                })
                : null
        };

        await using var issuer = harness.CreateService(mediator);
        var issued = await issuer.IssueBillingRunAsync(run.Id);

        Assert.True(issued.IsSuccess);
        var submitted = Assert.Single(mediator.Sent.OfType<SubmitInvoiceCommand>());
        Assert.Equal(draft.Id, submitted.DraftId);
        Assert.Equal(RecurringBillingIssueKeys.ForRun(run.Id), submitted.IdempotencyKey);
    }

    private static async Task<(RecurringContractBillingRun Run, InvoiceDraft Draft)> SeedDraftCreatedAsync(
        RecurringContractTestHarness harness,
        IReadOnlyList<DraftInvoiceLine> lines)
    {
        var client = await harness.SeedClientAsync($"Client {Guid.NewGuid():N}"[..20]);
        var contract = await harness.SeedContractAsync(client.Id,
            configure: c => c.Activate(),
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m));
        var draft = await harness.SeedInvoiceDraftAsync(lines);
        var run = await harness.SeedRunAsync(
            contract.Id, new DateTime(2026, 6, 1), new DateTime(2026, 6, 30),
            RecurringContractBillingRunStatus.DraftCreated, fixedAmount: 100m, invoiceDraftId: draft.Id);
        return (run, draft);
    }
}
