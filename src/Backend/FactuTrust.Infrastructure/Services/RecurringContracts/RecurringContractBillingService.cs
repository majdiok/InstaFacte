using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.InvoiceWizard.Commands;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Entities.RecurringContracts;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services;
using FactuTrust.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FactuTrust.Infrastructure.Services.RecurringContracts;

public sealed class RecurringContractBillingService
{
    private readonly IMediator _mediator;
    private readonly RecurringContractsOptions _options;
    private readonly ILogger<RecurringContractBillingService> _logger;

    public RecurringContractBillingService(
        IMediator mediator,
        IOptions<RecurringContractsOptions> options,
        ILogger<RecurringContractBillingService> logger)
    {
        _mediator = mediator;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result<int>> ScanAndCreateDraftsAsync(
        TenantDbContext db,
        Guid? contractIdFilter,
        DateTime asOfDate,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled || !_options.BillingJobEnabled)
            return Result.Success(0);

        var threshold = asOfDate.Date.AddDays(_options.BillingWindowDays);
        var q = db.RecurringContracts
            .Include(c => c.Lines)
            .Where(c => c.Status == RecurringContractStatus.Active
                && c.NextBillingDate != null
                && c.NextBillingDate.Value.Date <= threshold);

        if (contractIdFilter.HasValue)
            q = q.Where(c => c.Id == contractIdFilter.Value);

        var contracts = await q.ToListAsync(cancellationToken);
        var created = 0;

        foreach (var contract in contracts)
        {
            try
            {
                var result = await ProcessContractAsync(db, contract, asOfDate, cancellationToken);
                if (result) created++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Billing failed for contract {ContractId}", contract.Id);
            }
        }

        return Result.Success(created);
    }

    private async Task<bool> ProcessContractAsync(
        TenantDbContext db,
        RecurringContract contract,
        DateTime asOfDate,
        CancellationToken cancellationToken)
    {
        if (!contract.CanBillForPeriod())
            return false;

        var (periodFrom, periodTo) = contract.GetCurrentBillingPeriod();

        var existingRun = await db.RecurringContractBillingRuns
            .FirstOrDefaultAsync(r =>
                r.RecurringContractId == contract.Id &&
                r.PeriodFrom == periodFrom &&
                r.PeriodTo == periodTo, cancellationToken);

        if (existingRun is not null &&
            existingRun.Status is RecurringContractBillingRunStatus.DraftCreated
                or RecurringContractBillingRunStatus.Invoiced)
            return false;

        var billingRun = existingRun ?? RecurringContractBillingRun.CreatePending(contract.Id, periodFrom, periodTo);
        if (existingRun is null)
            await db.RecurringContractBillingRuns.AddAsync(billingRun, cancellationToken);

        var amounts = await ComputeAmountsAsync(db, contract, periodFrom, periodTo, cancellationToken);
        var draftId = await CreateInvoiceDraftAsync(db, contract, billingRun, amounts, cancellationToken);
        if (!draftId.HasValue)
        {
            billingRun.MarkFailed("Échec de création du brouillon de facture");
            await db.SaveChangesAsync(cancellationToken);
            return false;
        }

        billingRun.MarkDraftCreated(draftId.Value, amounts.FixedAmount, amounts.UsageAmount, amounts.ProrationAmount);
        contract.AdvanceBillingSchedule();

        if (amounts.HasOneTimeSetup && !contract.SetupFeeBilled)
            contract.MarkSetupFeeBilled();

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<BillingAmounts> ComputeAmountsAsync(
        TenantDbContext db,
        RecurringContract contract,
        DateTime periodFrom,
        DateTime periodTo,
        CancellationToken cancellationToken)
    {
        decimal fixedAmount = 0m;
        decimal usageAmount = 0m;
        decimal prorationAmount = 0m;
        var hasOneTime = false;

        var activeLines = contract.GetActiveLinesOn(periodTo).ToList();

        foreach (var line in activeLines)
        {
            switch (line.LineType)
            {
                case RecurringContractLineType.OneTimeSetup:
                    if (!contract.SetupFeeBilled)
                    {
                        fixedAmount += line.Quantity * line.UnitPriceHT;
                        hasOneTime = true;
                    }
                    break;

                case RecurringContractLineType.FixedRecurring:
                {
                    var full = line.Quantity * line.UnitPriceHT;
                    var prorated = ProrateLineAmount(contract, line, periodFrom, periodTo, full);
                    fixedAmount += prorated;
                    prorationAmount += prorated - full;
                    break;
                }

                case RecurringContractLineType.UsageMetered:
                    usageAmount += await ComputeUsageAmountAsync(db, contract.Id, line, periodFrom, periodTo, cancellationToken);
                    break;
            }
        }

        return new BillingAmounts(fixedAmount, usageAmount, prorationAmount, hasOneTime);
    }

    // Délègue au domaine partagé (comportement strictement identique — voir RecurringContractLineProrationTests).
    private static decimal ProrateLineAmount(
        RecurringContract contract,
        RecurringContractLine line,
        DateTime periodFrom,
        DateTime periodTo,
        decimal fullAmount) =>
        RecurringContractLineProration.Prorate(
            periodFrom, periodTo,
            contract.StartDate, contract.EndDate,
            line.EffectiveFrom, line.EffectiveTo,
            fullAmount);

    private static async Task<decimal> ComputeUsageAmountAsync(
        TenantDbContext db,
        Guid contractId,
        RecurringContractLine line,
        DateTime periodFrom,
        DateTime periodTo,
        CancellationToken cancellationToken)
    {
        if (!line.UsageMetricId.HasValue)
            return 0m;

        var metric = await db.UsageMetrics.AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == line.UsageMetricId.Value, cancellationToken);
        if (metric is null) return 0m;

        var records = await db.UsageRecords.AsNoTracking()
            .Where(r => r.RecurringContractId == contractId
                && r.UsageMetricId == line.UsageMetricId
                && r.PeriodFrom >= periodFrom
                && r.PeriodTo <= periodTo)
            .Select(r => r.Quantity)
            .ToListAsync(cancellationToken);

        if (records.Count == 0) return 0m;

        var quantity = metric.AggregationMode switch
        {
            UsageAggregationMode.Max => records.Max(),
            UsageAggregationMode.Last => records[^1],
            _ => records.Sum()
        };

        var included = line.IncludedQuantity ?? 0m;
        var overage = Math.Max(0m, quantity - included);
        var baseAmount = Math.Min(quantity, included) * line.UnitPriceHT;
        var overageAmount = overage * (line.OverageUnitPriceHT ?? line.UnitPriceHT);
        return decimal.Round(baseAmount + overageAmount, 3, MidpointRounding.AwayFromZero);
    }

    private async Task<Guid?> CreateInvoiceDraftAsync(
        TenantDbContext db,
        RecurringContract contract,
        RecurringContractBillingRun billingRun,
        BillingAmounts amounts,
        CancellationToken cancellationToken)
    {
        var client = await db.Clients.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == contract.ClientId, cancellationToken);
        if (client is null) return null;

        var company = await db.Companies.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        var lines = BuildDraftLines(contract, billingRun, amounts);

        var issueDate = DateTime.UtcNow.Date;
        DateTime dueDate = issueDate.AddDays(30);
        int? daysUntilDue = 30;
        string? paymentTerms = null;

        if (contract.PaymentTermTemplateId.HasValue)
        {
            var template = await db.PaymentTermTemplates.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == contract.PaymentTermTemplateId.Value, cancellationToken);
            if (template is not null)
            {
                dueDate = template.ComputeDueDate(issueDate);
                daysUntilDue = Math.Max(0, (dueDate.Date - issueDate).Days);
                paymentTerms = template.ToDocumentLabel();
            }
        }

        Guid? warehouseId = await db.Warehouses.AsNoTracking()
            .Where(w => w.IsDefault && w.IsActive)
            .Select(w => (Guid?)w.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var request = new SaveDraftRequest
        {
            Metadata = new WizardStepMetadataDto
            {
                Type = "INVOICE",
                IssueDate = issueDate,
                DueDate = dueDate,
                Currency = contract.Currency,
                InternalReference = $"Contrat {contract.Number} — {billingRun.PeriodFrom:dd/MM/yyyy} au {billingRun.PeriodTo:dd/MM/yyyy}",
                WarehouseId = warehouseId
            },
            SellerId = company?.Id,
            Client = new WizardStepClientDto
            {
                ClientId = contract.ClientId,
                IsNewClient = false
            },
            Lines = lines,
            PaymentLegal = new WizardStepPaymentLegalDto
            {
                PaymentMethod = "BANK_TRANSFER",
                PaymentTerms = paymentTerms,
                DaysUntilDue = daysUntilDue,
                BankInfo = company is null ? null : new WizardBankInfoDto
                {
                    BankName = company.BankName,
                    Iban = company.Iban,
                    Rib = company.Rib
                }
            },
            CurrentStep = 4
        };

        var result = await _mediator.Send(new SaveDraftCommand(request), cancellationToken);
        return result.IsSuccess ? result.Value.Id : null;
    }

    private static List<WizardStepLineDto> BuildDraftLines(
        RecurringContract contract,
        RecurringContractBillingRun billingRun,
        BillingAmounts amounts)
    {
        var lines = new List<WizardStepLineDto>();

        foreach (var contractLine in contract.GetActiveLinesOn(billingRun.PeriodTo))
        {
            if (contractLine.LineType == RecurringContractLineType.OneTimeSetup && contract.SetupFeeBilled)
                continue;

            if (contractLine.LineType == RecurringContractLineType.UsageMetered)
                continue;

            lines.Add(new WizardStepLineDto
            {
                ProductId = contractLine.ProductId?.ToString(),
                Designation = contractLine.Description,
                Quantity = contractLine.Quantity,
                UnitPriceHT = contractLine.UnitPriceHT,
                VatRate = (int)contractLine.VatRate,
                PriceOverridden = true
            });
        }

        if (amounts.UsageAmount > 0)
        {
            lines.Add(new WizardStepLineDto
            {
                Designation = $"Consommation période {billingRun.PeriodFrom:dd/MM/yyyy} - {billingRun.PeriodTo:dd/MM/yyyy}",
                Quantity = 1,
                UnitPriceHT = amounts.UsageAmount,
                VatRate = 19,
                PriceOverridden = true
            });
        }

        if (amounts.ProrationAmount != 0)
        {
            lines.Add(new WizardStepLineDto
            {
                Designation = "Ajustement prorata période",
                Quantity = 1,
                UnitPriceHT = amounts.ProrationAmount,
                VatRate = 19,
                PriceOverridden = true
            });
        }

        return lines;
    }

    private sealed record BillingAmounts(decimal FixedAmount, decimal UsageAmount, decimal ProrationAmount, bool HasOneTimeSetup);
}
