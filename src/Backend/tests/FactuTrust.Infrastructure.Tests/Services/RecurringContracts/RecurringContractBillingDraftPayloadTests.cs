using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.InvoiceWizard.Commands;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Entities.Pricing;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Services.RecurringContracts;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services.RecurringContracts;

public sealed class RecurringContractBillingDraftPayloadTests
{
    [Fact]
    public async Task ScanAndCreateDrafts_UsesPaymentTermDueDateAndCompanyBank()
    {
        var harness = new RecurringContractTestHarness();
        var client = await harness.SeedClientAsync("Client échéance");
        await harness.SeedCompanyAsync();

        await using var seed = harness.Factory.CreateContext();
        var template = PaymentTermTemplate.Create("30 jours net", 30).Value;
        seed.PaymentTermTemplates.Add(template);
        var warehouse = Warehouse.Create("PRINCIPAL", "Entrepôt Principal", isDefault: true).Value;
        seed.Warehouses.Add(warehouse);
        await seed.SaveChangesAsync();

        var today = DateTime.UtcNow.Date;
        var contract = await harness.SeedContractAsync(
            client.Id,
            startDate: today,
            billingDay: today.Day,
            paymentTermTemplateId: template.Id,
            configure: c => c.Activate(),
            lines: c => c.AddLine(RecurringContractLineType.FixedRecurring, "Abonnement", 1, 55m, 19m, RecurringContractTestHarness.DefaultProductId));

        var mediator = new RecordingMediator
        {
            OnSend = request =>
            {
                if (request is SaveDraftCommand)
                {
                    return Result.Success(new DraftResponseDto
                    {
                        Id = Guid.NewGuid(),
                        CurrentStep = 4,
                        LastModifiedAt = DateTime.UtcNow,
                        ExpiresAt = DateTime.UtcNow.AddDays(7)
                    });
                }

                return null;
            }
        };

        var billing = new RecurringContractBillingService(
            mediator,
            Options.Create(new RecurringContractsOptions
            {
                Enabled = true,
                BillingJobEnabled = true,
                BillingWindowDays = 3
            }),
            NullLogger<RecurringContractBillingService>.Instance);

        await using var db = harness.Factory.CreateContext();
        var created = await billing.ScanAndCreateDraftsAsync(db, contract.Id, today);

        Assert.True(created.IsSuccess);
        Assert.Equal(1, created.Value);
        var save = Assert.Single(mediator.Sent.OfType<SaveDraftCommand>());
        var meta = save.Request.Metadata!;
        Assert.Equal(today, meta.IssueDate.Date);
        Assert.Equal(template.ComputeDueDate(today), meta.DueDate!.Value.Date);
        Assert.Equal(warehouse.Id, meta.WarehouseId);
        Assert.Equal("BANK_TRANSFER", save.Request.PaymentLegal!.PaymentMethod);
        Assert.Equal(template.ToDocumentLabel(), save.Request.PaymentLegal.PaymentTerms);
        Assert.Equal("BIAT", save.Request.PaymentLegal.BankInfo!.BankName);
        Assert.Equal("TN5904018104004942712345", save.Request.PaymentLegal.BankInfo.Iban);
        Assert.Equal("04018104004942712345", save.Request.PaymentLegal.BankInfo.Rib);
    }
}
