using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.InvoiceWizard.Commands;
using FactuTrust.Application.Features.RecurringContracts;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services.RecurringContracts;

public sealed class RecurringContractIssueTests
{
    [Fact]
    public async Task IssueBillingRun_DraftCreated_SendsSubmitInvoiceWithStableKey()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client émission");
        var contract = await harness.SeedContractAsync(client.Id,
            startDate: new DateTime(2026, 1, 1),
            configure: c => c.Activate(),
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m));
        var draftId = Guid.NewGuid();
        var run = await harness.SeedRunAsync(
            contract.Id, new DateTime(2026, 1, 1), new DateTime(2026, 1, 31),
            RecurringContractBillingRunStatus.DraftCreated, fixedAmount: 100m, invoiceDraftId: draftId);

        var invoiceId = Guid.NewGuid();
        var mediator = new RecordingMediator
        {
            OnSend = request => request is SubmitInvoiceCommand
                ? Result.Success(new InvoiceCreatedResultDto
                {
                    InvoiceId = invoiceId,
                    InvoiceNumber = "FAC-2026-0001",
                    Status = "Validée",
                    CreatedAt = DateTime.UtcNow
                })
                : null
        };

        await using var sut = harness.CreateService(mediator);
        var result = await sut.IssueBillingRunAsync(run.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(invoiceId, result.Value.InvoiceId);
        Assert.Equal("FAC-2026-0001", result.Value.InvoiceNumber);
        var submitted = Assert.Single(mediator.Sent.OfType<SubmitInvoiceCommand>());
        Assert.Equal(draftId, submitted.DraftId);
        Assert.Equal(RecurringBillingIssueKeys.ForRun(run.Id), submitted.IdempotencyKey);
        Assert.True(submitted.IdempotencyKey.Length >= 32);
    }

    [Fact]
    public async Task IssueBillingRun_Pending_DoesNotSubmit()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client pending");
        var contract = await harness.SeedContractAsync(client.Id,
            configure: c => c.Activate(),
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m));
        var run = await harness.SeedRunAsync(
            contract.Id, new DateTime(2026, 2, 1), new DateTime(2026, 2, 28),
            RecurringContractBillingRunStatus.Pending);

        var mediator = new RecordingMediator();
        await using var sut = harness.CreateService(mediator);
        var result = await sut.IssueBillingRunAsync(run.Id);

        Assert.True(result.IsFailure);
        Assert.Empty(mediator.Sent);
    }

    [Fact]
    public async Task IssueBillingRun_AlreadyInvoiced_DoesNotSubmit()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client invoiced");
        var contract = await harness.SeedContractAsync(client.Id,
            configure: c => c.Activate(),
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m));
        var run = await harness.SeedRunAsync(
            contract.Id, new DateTime(2026, 3, 1), new DateTime(2026, 3, 31),
            RecurringContractBillingRunStatus.Invoiced, invoiceId: Guid.NewGuid());

        var mediator = new RecordingMediator();
        await using var sut = harness.CreateService(mediator);
        var result = await sut.IssueBillingRunAsync(run.Id);

        Assert.True(result.IsFailure);
        Assert.Empty(mediator.Sent);
    }

    [Fact]
    public async Task IssueBillingRun_UnknownId_ReturnsNotFound()
    {
        var harness = new RecurringContractTestHarness();
        var mediator = new RecordingMediator();
        await using var sut = harness.CreateService(mediator);
        var result = await sut.IssueBillingRunAsync(Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Contains("NotFound", result.Error.Code, StringComparison.Ordinal);
        Assert.Empty(mediator.Sent);
    }

    [Fact]
    public async Task IssueBillingRun_QuotaExceeded_DoesNotSubmit()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client quota");
        var contract = await harness.SeedContractAsync(client.Id,
            configure: c => c.Activate(),
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m));
        var run = await harness.SeedRunAsync(
            contract.Id, new DateTime(2026, 4, 1), new DateTime(2026, 4, 30),
            RecurringContractBillingRunStatus.DraftCreated, invoiceDraftId: Guid.NewGuid());

        var mediator = new RecordingMediator();
        await using var sut = harness.CreateService(mediator, new DenyingPlanQuota());
        var result = await sut.IssueBillingRunAsync(run.Id);

        Assert.True(result.IsFailure);
        Assert.Contains("Limite mensuelle", result.Error.Description);
        Assert.Empty(mediator.Sent);
    }

    [Fact]
    public async Task IssueBillingRun_SecondCallWhileStillDraftCreated_ReusesSameIdempotencyKey()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client double");
        var contract = await harness.SeedContractAsync(client.Id,
            configure: c => c.Activate(),
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 100m, 19m));
        var draftId = Guid.NewGuid();
        var run = await harness.SeedRunAsync(
            contract.Id, new DateTime(2026, 5, 1), new DateTime(2026, 5, 31),
            RecurringContractBillingRunStatus.DraftCreated, invoiceDraftId: draftId);

        var invoiceId = Guid.NewGuid();
        var mediator = new RecordingMediator
        {
            OnSend = request => request is SubmitInvoiceCommand
                ? Result.Success(new InvoiceCreatedResultDto
                {
                    InvoiceId = invoiceId,
                    InvoiceNumber = "FAC-2026-0042",
                    Status = "Validée",
                    CreatedAt = DateTime.UtcNow
                })
                : null
        };

        await using var sut = harness.CreateService(mediator);
        var first = await sut.IssueBillingRunAsync(run.Id);
        var second = await sut.IssueBillingRunAsync(run.Id);

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(invoiceId, first.Value.InvoiceId);
        Assert.Equal(invoiceId, second.Value.InvoiceId);
        var keys = mediator.Sent.OfType<SubmitInvoiceCommand>().Select(c => c.IdempotencyKey).ToList();
        Assert.Equal(2, keys.Count);
        Assert.All(keys, k => Assert.Equal(RecurringBillingIssueKeys.ForRun(run.Id), k));
    }
}
