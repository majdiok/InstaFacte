using FactuTrust.Application.Common;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.Services;
using FactuTrust.Application.Features.Reports.Queries;
using FactuTrust.Application.Features.WithholdingTax.Queries;
using FactuTrust.Domain.Authorization;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Accounting.Queries;

/// <summary>
/// Quels montants la déclaration doit porter.
/// </summary>
public enum VatDeclarationValuation
{
    /// <summary>
    /// Ce qui a été déposé : si une déclaration existe en base, ses montants font foi et rien ne
    /// bouge derrière le dos de l'utilisateur. Mode de l'écran de saisie et des deux PDF, pour
    /// qu'un document édité ne diverge jamais du dépôt qu'il représente.
    /// </summary>
    Declared = 0,

    /// <summary>
    /// Ce que les modules produisent aujourd'hui, en ignorant la déclaration enregistrée.
    /// Réservé aux appelants qui ont besoin du recalcul : la sauvegarde (qui rafraîchit la TVA
    /// depuis les écritures) et l'estimation du tableau de bord.
    /// </summary>
    Live = 1
}

/// <param name="EnforceCompanySubmittedOnly">
/// Quand true (GET/PDF HTTP société), n'expose que les déclarations Soumise/Verrouillée.
/// Laisser false pour les appels internes (save, reporting) afin de ne pas casser le recalcul.
/// </param>
/// <param name="Valuation">Voir <see cref="VatDeclarationValuation"/>. Par défaut : les montants déposés.</param>
public sealed record GetVatDeclarationQuery(
    int Year,
    int Month,
    bool EnforceCompanySubmittedOnly = false,
    VatDeclarationValuation Valuation = VatDeclarationValuation.Declared) : IRequest<Result<VatDeclarationDto>>;

public sealed class GetVatDeclarationQueryHandler : IRequestHandler<GetVatDeclarationQuery, Result<VatDeclarationDto>>
{
    private static readonly int[] TunisiaVatRates = [19, 13, 7];

    private readonly IMediator _mediator;
    private readonly IVatDeclarationRepository _vatDeclarationRepository;
    private readonly IJournalEntryRepository _journalEntries;
    private readonly IInvoiceRepository _invoices;
    private readonly ISupplierInvoiceRepository _supplierInvoices;
    private readonly ITenantCompanySummaryProvider _tenantCompanySummary;
    private readonly PayrollDeclarationContributionProvider _payroll;
    private readonly AccountingSettings _settings;

    public GetVatDeclarationQueryHandler(
        IMediator mediator,
        IVatDeclarationRepository vatDeclarationRepository,
        IJournalEntryRepository journalEntries,
        IInvoiceRepository invoices,
        ISupplierInvoiceRepository supplierInvoices,
        ITenantCompanySummaryProvider tenantCompanySummary,
        PayrollDeclarationContributionProvider payroll,
        IOptions<AccountingSettings> settings)
    {
        _mediator = mediator;
        _vatDeclarationRepository = vatDeclarationRepository;
        _journalEntries = journalEntries;
        _invoices = invoices;
        _supplierInvoices = supplierInvoices;
        _tenantCompanySummary = tenantCompanySummary;
        _payroll = payroll;
        _settings = settings.Value;
    }

    public async Task<Result<VatDeclarationDto>> Handle(GetVatDeclarationQuery request, CancellationToken cancellationToken)
    {
        // Filtre société : early-return avant tout recalcul pour ne pas fuiter les montants d'un brouillon.
        if (request.EnforceCompanySubmittedOnly)
        {
            var companyVisible = await _vatDeclarationRepository.GetByYearMonthAsync(
                request.Year, request.Month, cancellationToken);
            if (companyVisible is null || !VatDeclarationAccess.IsVisibleToCompany(companyVisible.Status))
                return Result.Failure<VatDeclarationDto>(VatDeclarationAccess.NotSubmitted());
        }

        var v2 = _settings.MonthlyDeclarationV2Enabled;

        var context = await BuildPeriodContextAsync(request, cancellationToken);
        if (context.Error is not null)
            return Result.Failure<VatDeclarationDto>(context.Error);

        var saved = await _vatDeclarationRepository.GetByYearMonthAsync(request.Year, request.Month, cancellationToken);

        // Ce que les modules produisent aujourd'hui. Calculé dans tous les cas : même quand la
        // déclaration est déposée et fait foi, l'écart doit rester visible.
        var live = await ComputeLiveAsync(request, context, v2, cancellationToken);

        // Ce que la déclaration porte réellement. Une déclaration enregistrée fait foi, y compris
        // pour la TVA : un document déposé ne se réécrit pas tout seul entre deux affichages.
        var declared = saved is not null && request.Valuation == VatDeclarationValuation.Declared
            ? TaxAmounts.FromEntity(saved)
            : live;

        var tenantSummary = await _tenantCompanySummary.GetCurrentTenantSummaryAsync(cancellationToken);

        var dto = new VatDeclarationDto
        {
            Year = request.Year,
            Month = request.Month,
            CollectedVat19 = declared.CollectedVat19,
            CollectedVat13 = declared.CollectedVat13,
            CollectedVat7 = declared.CollectedVat7,
            DeductibleVatGoods = declared.DeductibleVatGoods,
            DeductibleVatAssets = declared.DeductibleVatAssets,
            PreviousCredit = declared.PreviousCredit,
            VatDue = declared.VatDue,
            CreditToCarry = declared.CreditToCarry,
            Currency = context.Currency,
            Status = saved != null ? (int)saved.Status : (int)VatDeclarationStatus.Draft,
            Fodec = declared.Fodec,
            DroitTimbre = declared.DroitTimbre,
            Tcl = declared.Tcl,
            Tfp = declared.Tfp,
            Foprolos = declared.Foprolos,
            WithholdingTax = declared.WithholdingTax,
            Acomptes = declared.Acomptes,
            TotalToPay = TotalToPay(declared, v2),
            Version = saved?.RevisionNumber ?? 1,
            IsRectificative = saved?.IsRectificative ?? false,
            MonthlyDeclarationV2Enabled = v2,
            CreatedAt = saved?.CreatedAt,
            UpdatedAt = saved?.UpdatedAt,
            SubmittedAt = saved?.SubmittedAt,
            CreatedBy = saved?.CreatedBy,
            UpdatedBy = saved?.UpdatedBy,
            FilingDeadline = VatFilingDeadline.ForPeriod(request.Year, request.Month),
            DeclarationTypeDisplay = v2 ? "Déclaration mensuelle unique" : "Déclaration TVA",
            CollectedVatBreakdown = context.CollectedVatBreakdown,
            DeductiblePurchasesTaxableBase = context.DeductiblePurchasesTaxableBase,
            SalesTaxableBase = context.SalesTaxableBase,
            SalesGrossBase = context.SalesGrossBase,
            FodecTaxableBase = context.FodecTaxableBase,
            FodecRatePercent = _settings.FodecRatePercent,
            TclRatePercent = _settings.TclRatePercent,
            // Assiette et taux des taxes sur salaires : contexte, pas montant déclaré. Ils
            // décrivent la paie du mois et valent donc pour les deux valorisations.
            PayrollTaxBase = context.Payroll.TaxBase,
            TfpRatePercent = context.Payroll.TfpRatePercent,
            FoprolosRatePercent = context.Payroll.FoprolosRatePercent,
            PayrollSalariesGrossBase = context.Payroll.SalariesGrossBase,
            PayrollSalariesNetTaxableBase = context.Payroll.SalariesNetTaxableBase,
            PayrollWithholdingIrpp = context.Payroll.WithholdingIrpp,
            PayrollWithholdingCss = context.Payroll.WithholdingCss,
            CompanyName = tenantSummary?.CompanyName ?? string.Empty,
            Nif = tenantSummary?.Nif ?? string.Empty,
            TaxRegimeDisplay = tenantSummary?.TaxRegimeDisplay ?? string.Empty,
            TradeName = tenantSummary?.TradeName,
            // Déjà chargée par le provider mais jusqu'ici non remontée : l'en-tête du formulaire
            // officiel en a besoin.
            AddressLine = tenantSummary?.AddressLine,
            OfficialFormEnabled = _settings.MonthlyDeclarationOfficialFormEnabled,
            Suggested = v2 ? MapComputed(live, context, v2) : null
        };

        return Result.Success(dto);
    }

    // ── Contexte de période : sources communes aux deux valorisations ──────────

    /// <summary>
    /// Assiettes et ventilations de la période. Toujours vivantes : ce sont des éléments de
    /// contexte (base × taux affichés en regard d'un montant), jamais des montants déclarés.
    /// </summary>
    private sealed record PeriodContext
    {
        public Error? Error { get; init; }
        public string Currency { get; init; } = Money.DefaultCurrency;
        public decimal CollectedVat19 { get; init; }
        public decimal CollectedVat13 { get; init; }
        public decimal CollectedVat7 { get; init; }
        public decimal DeductibleVatGoods { get; init; }
        public decimal DeductibleVatAssets { get; init; }
        public decimal PreviousCredit { get; init; }
        public decimal SalesTaxableBase { get; init; }
        public decimal SalesGrossBase { get; init; }
        public decimal DeductiblePurchasesTaxableBase { get; init; }
        public decimal FodecTaxableBase { get; init; }
        public PayrollMonthlyContribution Payroll { get; init; } = PayrollMonthlyContribution.None;
        public IReadOnlyList<VatRateBreakdownDto> CollectedVatBreakdown { get; init; } = Array.Empty<VatRateBreakdownDto>();
    }

    private async Task<PeriodContext> BuildPeriodContextAsync(
        GetVatDeclarationQuery request, CancellationToken cancellationToken)
    {
        var start = new DateTime(request.Year, request.Month, 1);
        var end = start.AddMonths(1).AddDays(-1);

        // Assiette légale : seules les factures éligibles (hors brouillon/annulée) alimentent la
        // déclaration, contrairement aux états de consultation qui affichent tout.
        var sales = await _mediator.Send(new GetSalesVatReportQuery(start, end, RealizedOnly: true), cancellationToken);
        if (sales.IsFailure)
            return new PeriodContext { Error = sales.Error };

        var purchases = await _mediator.Send(new GetPurchasesVatReportQuery(start, end, RealizedOnly: true), cancellationToken);
        if (purchases.IsFailure)
            return new PeriodContext { Error = purchases.Error };

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

        var v2 = _settings.MonthlyDeclarationV2Enabled;

        return new PeriodContext
        {
            Currency = sales.Value.FirstOrDefault()?.Currency ?? Money.DefaultCurrency,
            CollectedVat19 = c19,
            CollectedVat13 = c13,
            CollectedVat7 = c7,
            DeductibleVatGoods = dedGoods,
            DeductibleVatAssets = dedAssets,
            PreviousCredit = prev?.CreditToCarry.Amount ?? 0,
            SalesTaxableBase = sales.Value.Sum(r => r.TotalTaxableAmount),
            SalesGrossBase = sales.Value.Sum(r => r.TotalTaxableAmount + r.TotalVatAmount),
            DeductiblePurchasesTaxableBase = purchases.Value.Sum(p => p.TotalTaxableAmount),
            FodecTaxableBase = v2 ? await _invoices.SumFodecTaxableBaseAsync(start, end, cancellationToken) : 0m,
            Payroll = v2 ? await _payroll.GetAsync(request.Year, request.Month, cancellationToken) : PayrollMonthlyContribution.None,
            CollectedVatBreakdown = TunisiaVatRates
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
                .ToList()
        };
    }

    // ── Recalcul temps réel ────────────────────────────────────────────────────

    private async Task<TaxAmounts> ComputeLiveAsync(
        GetVatDeclarationQuery request, PeriodContext context, bool v2, CancellationToken cancellationToken)
    {
        var start = new DateTime(request.Year, request.Month, 1);
        var end = start.AddMonths(1).AddDays(-1);

        var draft = VatDeclaration.CreateDraft(
            request.Year,
            request.Month,
            Money.Create(context.CollectedVat19, context.Currency),
            Money.Create(context.CollectedVat13, context.Currency),
            Money.Create(context.CollectedVat7, context.Currency),
            Money.Create(context.DeductibleVatGoods, context.Currency),
            Money.Create(context.DeductibleVatAssets, context.Currency),
            Money.Create(context.PreviousCredit, context.Currency),
            context.Currency);

        var vat = new TaxAmounts
        {
            CollectedVat19 = draft.CollectedVat19.Amount,
            CollectedVat13 = draft.CollectedVat13.Amount,
            CollectedVat7 = draft.CollectedVat7.Amount,
            DeductibleVatGoods = draft.DeductibleVatGoods.Amount,
            DeductibleVatAssets = draft.DeductibleVatAssets.Amount,
            PreviousCredit = draft.PreviousCredit.Amount,
            VatDue = draft.VatDue.Amount,
            CreditToCarry = draft.CreditToCarry.Amount
        };

        if (!v2)
            return vat;

        // Retenue à la source : factures fournisseurs (module RS) et traitements et salaires
        // (IRPP + CSS du cycle de paie). Les deux relèvent de la même ligne de la déclaration.
        var rs = await _mediator.Send(new GetWithholdingMonthlyReportQuery(request.Year, request.Month), cancellationToken);
        var withholdingFromInvoices = rs.GrandTotalWithheld;
        var withholdingFromSalaries = context.Payroll.WithholdingTotal;

        return vat with
        {
            Fodec = await _invoices.SumFodecAsync(start, end, cancellationToken),
            DroitTimbre = await _invoices.SumFiscalStampAsync(start, end, cancellationToken),
            Tcl = Math.Round(_settings.TclRatePercent / 100m * context.SalesGrossBase, 3),
            Tfp = context.Payroll.Tfp,
            Foprolos = context.Payroll.Foprolos,
            WithholdingFromInvoices = withholdingFromInvoices,
            WithholdingFromSalaries = withholdingFromSalaries,
            WithholdingTax = withholdingFromInvoices + withholdingFromSalaries
            // Acomptes provisionnels : aucune source automatique, saisie manuelle uniquement.
        };
    }

    private static decimal TotalToPay(TaxAmounts a, bool v2) => v2
        ? Math.Max(0m, a.VatDue + a.Fodec + a.DroitTimbre + a.Tcl + a.Tfp + a.Foprolos + a.WithholdingTax - a.Acomptes)
        : a.VatDue;

    private static VatDeclarationComputedDto MapComputed(TaxAmounts live, PeriodContext context, bool v2) => new()
    {
        CollectedVat19 = live.CollectedVat19,
        CollectedVat13 = live.CollectedVat13,
        CollectedVat7 = live.CollectedVat7,
        DeductibleVatGoods = live.DeductibleVatGoods,
        DeductibleVatAssets = live.DeductibleVatAssets,
        PreviousCredit = live.PreviousCredit,
        VatDue = live.VatDue,
        CreditToCarry = live.CreditToCarry,
        Fodec = live.Fodec,
        DroitTimbre = live.DroitTimbre,
        Tcl = live.Tcl,
        Tfp = live.Tfp,
        Foprolos = live.Foprolos,
        WithholdingTax = live.WithholdingTax,
        WithholdingFromInvoices = live.WithholdingFromInvoices,
        WithholdingFromSalaries = live.WithholdingFromSalaries,
        TotalToPay = TotalToPay(live, v2),
        PayrollRunExists = context.Payroll.RunExists,
        PayrollRunStatus = context.Payroll.Status is { } s ? (int)s : null,
        PayrollRunStatusDisplay = context.Payroll.Status?.ToDisplayString(),
        PayrollRunUsable = context.Payroll.IsUsable
    };

    /// <summary>
    /// Jeu de montants d'une déclaration, qu'il vienne du recalcul ou du dépôt enregistré. Deux
    /// instances de même forme rendent l'écart calculable ligne à ligne.
    /// </summary>
    private sealed record TaxAmounts
    {
        public decimal CollectedVat19 { get; init; }
        public decimal CollectedVat13 { get; init; }
        public decimal CollectedVat7 { get; init; }
        public decimal DeductibleVatGoods { get; init; }
        public decimal DeductibleVatAssets { get; init; }
        public decimal PreviousCredit { get; init; }
        public decimal VatDue { get; init; }
        public decimal CreditToCarry { get; init; }
        public decimal Fodec { get; init; }
        public decimal DroitTimbre { get; init; }
        public decimal Tcl { get; init; }
        public decimal Tfp { get; init; }
        public decimal Foprolos { get; init; }
        public decimal WithholdingTax { get; init; }
        public decimal WithholdingFromInvoices { get; init; }
        public decimal WithholdingFromSalaries { get; init; }
        public decimal Acomptes { get; init; }

        public static TaxAmounts FromEntity(VatDeclaration d) => new()
        {
            CollectedVat19 = d.CollectedVat19.Amount,
            CollectedVat13 = d.CollectedVat13.Amount,
            CollectedVat7 = d.CollectedVat7.Amount,
            DeductibleVatGoods = d.DeductibleVatGoods.Amount,
            DeductibleVatAssets = d.DeductibleVatAssets.Amount,
            PreviousCredit = d.PreviousCredit.Amount,
            VatDue = d.VatDue.Amount,
            CreditToCarry = d.CreditToCarry.Amount,
            Fodec = d.Fodec,
            DroitTimbre = d.DroitTimbre,
            Tcl = d.Tcl,
            Tfp = d.Tfp,
            Foprolos = d.Foprolos,
            WithholdingTax = d.WithholdingTax,
            // La déclaration ne mémorise pas la répartition de la RS : seul le total est déposé.
            Acomptes = d.Acomptes
        };
    }
}
