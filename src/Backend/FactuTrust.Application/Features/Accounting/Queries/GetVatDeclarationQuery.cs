using FactuTrust.Application.Common;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Reports.Queries;
using FactuTrust.Application.Features.WithholdingTax.Queries;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Accounting.Queries;

public sealed record GetVatDeclarationQuery(int Year, int Month) : IRequest<Result<VatDeclarationDto>>;

public sealed class GetVatDeclarationQueryHandler : IRequestHandler<GetVatDeclarationQuery, Result<VatDeclarationDto>>
{
    private static readonly int[] TunisiaVatRates = [19, 13, 7];

    private readonly IMediator _mediator;
    private readonly IVatDeclarationRepository _vatDeclarationRepository;
    private readonly IJournalEntryRepository _journalEntries;
    private readonly IInvoiceRepository _invoices;
    private readonly ISupplierInvoiceRepository _supplierInvoices;
    private readonly ITenantCompanySummaryProvider _tenantCompanySummary;
    private readonly IPayrollRunRepository _payrollRuns;
    private readonly AccountingSettings _settings;

    public GetVatDeclarationQueryHandler(
        IMediator mediator,
        IVatDeclarationRepository vatDeclarationRepository,
        IJournalEntryRepository journalEntries,
        IInvoiceRepository invoices,
        ISupplierInvoiceRepository supplierInvoices,
        ITenantCompanySummaryProvider tenantCompanySummary,
        IPayrollRunRepository payrollRuns,
        IOptions<AccountingSettings> settings)
    {
        _mediator = mediator;
        _vatDeclarationRepository = vatDeclarationRepository;
        _journalEntries = journalEntries;
        _invoices = invoices;
        _supplierInvoices = supplierInvoices;
        _tenantCompanySummary = tenantCompanySummary;
        _payrollRuns = payrollRuns;
        _settings = settings.Value;
    }

    public async Task<Result<VatDeclarationDto>> Handle(GetVatDeclarationQuery request, CancellationToken cancellationToken)
    {
        var start = new DateTime(request.Year, request.Month, 1);
        var end = start.AddMonths(1).AddDays(-1);

        // Assiette légale : seules les factures éligibles (hors brouillon/annulée) alimentent la
        // déclaration, contrairement aux états de consultation qui affichent tout.
        var sales = await _mediator.Send(new GetSalesVatReportQuery(start, end, RealizedOnly: true), cancellationToken);
        if (sales.IsFailure)
            return Result.Failure<VatDeclarationDto>(sales.Error);

        var purchases = await _mediator.Send(new GetPurchasesVatReportQuery(start, end, RealizedOnly: true), cancellationToken);
        if (purchases.IsFailure)
            return Result.Failure<VatDeclarationDto>(purchases.Error);

        var salesByRate = sales.Value.ToDictionary(r => r.VatRatePercent);
        decimal c19 = salesByRate.TryGetValue(19, out var r19) ? r19.TotalVatAmount : 0;
        decimal c13 = salesByRate.TryGetValue(13, out var r13) ? r13.TotalVatAmount : 0;
        decimal c7 = salesByRate.TryGetValue(7, out var r7) ? r7.TotalVatAmount : 0;

        var totalPurchaseVat = purchases.Value.Sum(p => p.TotalVatAmount);
        // TVA immobilisations : dérivée de la MÊME source que la TVA achats (lignes de facture
        // fournisseur marquées immobilisation), ce qui garantit qu'elle est un sous-ensemble du total
        // et évite la source croisée avec le journal. Repli sur le compte 43662 uniquement si aucune
        // ligne immobilisation n'est saisie sur la période (dossiers historiques non régressés).
        var assetVatFromInvoices = await _supplierInvoices.SumFixedAssetDeductibleVatAsync(start, end, realizedOnly: true, cancellationToken);
        var dedAssets = assetVatFromInvoices > 0
            ? assetVatFromInvoices
            : await _journalEntries.SumDebitsByAccountAsync("43662", start, end, cancellationToken);
        var dedGoods = Math.Max(0, totalPurchaseVat - dedAssets);

        var prevMonth = start.AddMonths(-1);
        var prev = await _vatDeclarationRepository.GetByYearMonthAsync(prevMonth.Year, prevMonth.Month, cancellationToken);
        var prevCredit = prev?.CreditToCarry.Amount ?? 0;

        var currency = sales.Value.FirstOrDefault()?.Currency ?? Money.DefaultCurrency;
        var salesTaxableBase = sales.Value.Sum(r => r.TotalTaxableAmount);
        var salesGrossBase = sales.Value.Sum(r => r.TotalTaxableAmount + r.TotalVatAmount);
        var deductiblePurchasesTaxableBase = purchases.Value.Sum(p => p.TotalTaxableAmount);

        var draft = VatDeclaration.CreateDraft(
            request.Year,
            request.Month,
            Money.Create(c19, currency),
            Money.Create(c13, currency),
            Money.Create(c7, currency),
            Money.Create(dedGoods, currency),
            Money.Create(dedAssets, currency),
            Money.Create(prevCredit, currency),
            currency);

        var saved = await _vatDeclarationRepository.GetByYearMonthAsync(request.Year, request.Month, cancellationToken);

        var v2 = _settings.MonthlyDeclarationV2Enabled;

        // Autres taxes : valeurs sauvegardées si la déclaration existe (saisies manuelles conservées),
        // sinon préremplissage — la RS vient du module Retenue à la Source quand la V2 est active.
        decimal fodec = 0, timbre = 0, tcl = 0, tfp = 0, foprolos = 0, withholding = 0, acomptes = 0;
        decimal fodecTaxableBase = 0;
        var version = 1;
        var isRectificative = false;

        if (v2)
            fodecTaxableBase = await _invoices.SumFodecTaxableBaseAsync(start, end, cancellationToken);

        if (saved is not null)
        {
            fodec = saved.Fodec; timbre = saved.DroitTimbre; tcl = saved.Tcl; tfp = saved.Tfp;
            foprolos = saved.Foprolos; withholding = saved.WithholdingTax; acomptes = saved.Acomptes;
            version = saved.RevisionNumber; isRectificative = saved.IsRectificative;
        }
        else if (v2)
        {
            var rs = await _mediator.Send(new GetWithholdingMonthlyReportQuery(request.Year, request.Month), cancellationToken);
            withholding = rs.GrandTotalWithheld;
            fodec = await _invoices.SumFodecAsync(start, end, cancellationToken);
            tcl = Math.Round(_settings.TclRatePercent / 100m * salesGrossBase, 3);
            timbre = await _invoices.SumFiscalStampAsync(start, end, cancellationToken);

            // Pré-remplissage TFP/FOPROLOS depuis la paie clôturée/validée du mois (module actif).
            var payrollRun = await _payrollRuns.GetByPeriodAsync(request.Year, request.Month, cancellationToken);
            if (payrollRun is { Status: PayrollRunStatus.Validated or PayrollRunStatus.Closed })
            {
                tfp = payrollRun.TotalTfp;
                foprolos = payrollRun.TotalFoprolos;
            }
        }

        var vatDue = draft.VatDue.Amount;
        var totalToPay = v2
            ? Math.Max(0m, vatDue + fodec + timbre + tcl + tfp + foprolos + withholding - acomptes)
            : vatDue;

        var collectedVatBreakdown = TunisiaVatRates
            .Select(rate =>
            {
                salesByRate.TryGetValue(rate, out var row);
                return new VatRateBreakdownDto
                {
                    RatePercent = rate,
                    TaxableBase = row?.TotalTaxableAmount ?? 0,
                    VatAmount = row?.TotalVatAmount ?? 0
                };
            })
            .ToList();

        var tenantSummary = await _tenantCompanySummary.GetCurrentTenantSummaryAsync(cancellationToken);

        var dto = new VatDeclarationDto
        {
            Year = request.Year,
            Month = request.Month,
            CollectedVat19 = draft.CollectedVat19.Amount,
            CollectedVat13 = draft.CollectedVat13.Amount,
            CollectedVat7 = draft.CollectedVat7.Amount,
            DeductibleVatGoods = draft.DeductibleVatGoods.Amount,
            DeductibleVatAssets = draft.DeductibleVatAssets.Amount,
            PreviousCredit = draft.PreviousCredit.Amount,
            VatDue = vatDue,
            CreditToCarry = draft.CreditToCarry.Amount,
            Currency = currency,
            Status = saved != null ? (int)saved.Status : (int)VatDeclarationStatus.Draft,
            Fodec = fodec,
            DroitTimbre = timbre,
            Tcl = tcl,
            Tfp = tfp,
            Foprolos = foprolos,
            WithholdingTax = withholding,
            Acomptes = acomptes,
            TotalToPay = totalToPay,
            Version = version,
            IsRectificative = isRectificative,
            MonthlyDeclarationV2Enabled = v2,
            CreatedAt = saved?.CreatedAt,
            UpdatedAt = saved?.UpdatedAt,
            SubmittedAt = saved?.SubmittedAt,
            CreatedBy = saved?.CreatedBy,
            UpdatedBy = saved?.UpdatedBy,
            FilingDeadline = VatFilingDeadline.ForPeriod(request.Year, request.Month),
            DeclarationTypeDisplay = v2 ? "Déclaration mensuelle unique" : "Déclaration TVA",
            CollectedVatBreakdown = collectedVatBreakdown,
            DeductiblePurchasesTaxableBase = deductiblePurchasesTaxableBase,
            SalesTaxableBase = salesTaxableBase,
            SalesGrossBase = salesGrossBase,
            FodecTaxableBase = fodecTaxableBase,
            FodecRatePercent = _settings.FodecRatePercent,
            TclRatePercent = _settings.TclRatePercent,
            CompanyName = tenantSummary?.CompanyName ?? string.Empty,
            Nif = tenantSummary?.Nif ?? string.Empty,
            TaxRegimeDisplay = tenantSummary?.TaxRegimeDisplay ?? string.Empty,
            TradeName = tenantSummary?.TradeName
        };

        return Result.Success(dto);
    }
}
