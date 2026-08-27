using FactuTrust.Application.Common;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.Queries;
using FactuTrust.Application.Features.Accounting.Services;
using FactuTrust.Application.Features.Reports.Queries;
using FactuTrust.Application.Features.WithholdingTax.Queries;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Entities.Payroll;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

/// <summary>
/// Contribution caisse à la déclaration TVA (plan §6.7) : <c>GetPostedCashSaleVatByRateAsync</c>
/// alimente <c>CollectedVat19/13/7</c> et <c>CollectedVatBreakdown</c>, jamais gardée par le flag
/// TVA (seule la création l'est), sans écrêtage des contributions négatives (extourne), et sans
/// jamais toucher aux bases facturières (<c>SalesTaxableBase</c>/<c>SalesGrossBase</c>/<c>Tcl</c>).
/// </summary>
public sealed class GetVatDeclarationCashVatTests
{
    private static Mock<ITenantCompanySummaryProvider> CreateTenantSummaryMock()
    {
        var mock = new Mock<ITenantCompanySummaryProvider>();
        mock.Setup(p => p.GetCurrentTenantSummaryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TenantCompanySummaryDto
            {
                CompanyName = "Ma Société SARL",
                Nif = "1234567/A/B/C/000",
                TaxRegimeDisplay = "Régime réel",
                TradeName = "Commerce"
            });
        return mock;
    }

    private static PayrollDeclarationContributionProvider CreatePayrollProvider()
    {
        var runs = new Mock<IPayrollRunRepository>();
        runs.Setup(r => r.GetByPeriodAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollRun?)null);
        var settings = new Mock<IPayrollParametersRepository>();
        settings.Setup(r => r.GetByFiscalYearAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollYearParameters?)null);
        return new PayrollDeclarationContributionProvider(runs.Object, settings.Object);
    }

    /// <summary>
    /// Fabrique un handler avec une base facturière fixe (1000 HT / 190 TVA @19%) et un mock du
    /// dépôt journal configurable pour la contribution caisse.
    /// </summary>
    private static GetVatDeclarationQueryHandler BuildHandler(
        IReadOnlyList<CashSaleVatPosting>? cashVat,
        bool cashVatSetup = true,
        decimal invoiceVat19 = 190m,
        decimal invoiceHt19 = 1000m)
    {
        var mediator = new Mock<IMediator>();
        var salesRows = new List<SalesVatReportRowDto>();
        if (invoiceVat19 != 0m || invoiceHt19 != 0m)
        {
            salesRows.Add(new() { VatRatePercent = 19, VatRateDisplay = "19 %", TotalVatAmount = invoiceVat19, TotalTaxableAmount = invoiceHt19, Currency = "TND" });
        }
        mediator.Setup(m => m.Send(It.IsAny<GetSalesVatReportQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<SalesVatReportRowDto>>(salesRows));
        mediator.Setup(m => m.Send(It.IsAny<GetPurchasesVatReportQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<PurchasesVatReportRowDto>>(new List<PurchasesVatReportRowDto>()));
        mediator.Setup(m => m.Send(It.IsAny<GetWithholdingMonthlyReportQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WithholdingMonthlyReportDto(2026, 7, new(), 0m, 0m, 0));

        var vatRepo = new Mock<IVatDeclarationRepository>();
        vatRepo.Setup(r => r.GetByYearMonthAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VatDeclaration?)null);

        var journals = new Mock<IJournalEntryRepository>();
        journals.Setup(r => r.SumDebitsByAccountAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);
        if (cashVatSetup)
        {
            journals.Setup(r => r.GetPostedCashSaleVatByRateAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(cashVat!); // null autorisé intentionnellement : simule un mock non configuré (cf. test JournalRepositoryReturnsNull)
        }

        var invoices = new Mock<IInvoiceRepository>();
        invoices.Setup(r => r.SumFodecTaxableBaseAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1000m);
        invoices.Setup(r => r.SumFodecAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);
        invoices.Setup(r => r.SumFiscalStampAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        var supplierInvoices = new Mock<ISupplierInvoiceRepository>();
        supplierInvoices.Setup(r => r.SumFixedAssetDeductibleVatAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        var settings = Options.Create(new AccountingSettings
        {
            MonthlyDeclarationV2Enabled = true,
            FodecRatePercent = 1.0m,
            TclRatePercent = 0.2m
        });

        return new GetVatDeclarationQueryHandler(
            mediator.Object, vatRepo.Object, journals.Object, invoices.Object, supplierInvoices.Object,
            CreateTenantSummaryMock().Object, CreatePayrollProvider(), settings);
    }

    [Fact]
    public async Task Handle_CashVatContribution_IncreasesCollectedVatByRateAndBreakdown()
    {
        var cashVat = new List<CashSaleVatPosting>
        {
            new(19, 100.000m, 19.000m),
            new(13, 10.000m, 1.300m),
            new(7, 10.000m, 0.700m)
        };
        var handler = BuildHandler(cashVat);

        var result = await handler.Handle(new GetVatDeclarationQuery(2026, 7), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var dto = result.Value;

        // Facturier (190 @19%) + caisse : 190 + 19 = 209 ; 0 + 1.3 = 1.3 ; 0 + 0.7 = 0.7.
        dto.CollectedVat19.Should().Be(209.000m);
        dto.CollectedVat13.Should().Be(1.300m);
        dto.CollectedVat7.Should().Be(0.700m);

        dto.CollectedVatBreakdown.Should().HaveCount(3);
        var b19 = dto.CollectedVatBreakdown.Single(b => b.RatePercent == 19);
        b19.TaxableBase.Should().Be(1100.000m); // 1000 facturier + 100 caisse
        b19.VatAmount.Should().Be(209.000m);

        var b13 = dto.CollectedVatBreakdown.Single(b => b.RatePercent == 13);
        b13.TaxableBase.Should().Be(10.000m);
        b13.VatAmount.Should().Be(1.300m);

        var b7 = dto.CollectedVatBreakdown.Single(b => b.RatePercent == 7);
        b7.TaxableBase.Should().Be(10.000m);
        b7.VatAmount.Should().Be(0.700m);
    }

    [Fact]
    public async Task Handle_CashVatContribution_DoesNotChangeSalesBasesOrTcl()
    {
        var withoutCash = await BuildHandler([]).Handle(new GetVatDeclarationQuery(2026, 7), CancellationToken.None);
        var withCash = await BuildHandler(new List<CashSaleVatPosting> { new(19, 100.000m, 19.000m) })
            .Handle(new GetVatDeclarationQuery(2026, 7), CancellationToken.None);

        withoutCash.IsSuccess.Should().BeTrue();
        withCash.IsSuccess.Should().BeTrue();

        // Bases facturières et TCL strictement inchangées malgré la contribution caisse (plan §6.7).
        withCash.Value.SalesTaxableBase.Should().Be(withoutCash.Value.SalesTaxableBase);
        withCash.Value.SalesGrossBase.Should().Be(withoutCash.Value.SalesGrossBase);
        withCash.Value.Tcl.Should().Be(withoutCash.Value.Tcl);

        withoutCash.Value.SalesTaxableBase.Should().Be(1000m);
        withoutCash.Value.SalesGrossBase.Should().Be(1190m);
    }

    [Fact]
    public async Task Handle_NegativeCashVatContribution_DecreasesCollectedVatWithoutClamping()
    {
        var cashVat = new List<CashSaleVatPosting> { new(19, -100.000m, -19.000m) };
        var handler = BuildHandler(cashVat);

        var result = await handler.Handle(new GetVatDeclarationQuery(2026, 7), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        // 190 (facturier) − 19 (extourne caisse) = 171, aucun écrêtage à zéro.
        result.Value.CollectedVat19.Should().Be(171.000m);

        var b19 = result.Value.CollectedVatBreakdown.Single(b => b.RatePercent == 19);
        b19.TaxableBase.Should().Be(900.000m);
        b19.VatAmount.Should().Be(171.000m);
    }

    /// <summary>
    /// Bug corrigé (revue de code) : sans TVA facturière pour compenser une extourne caisse, le
    /// NET recalculé (<c>ComputeLiveAsync</c>) est négatif. <c>Money.Create</c> rejetterait ce
    /// montant à la construction — le calcul doit rester en <c>Money.FromSignedAmount</c> pour ce
    /// mouvement et ne pas lever d'exception.
    /// </summary>
    [Fact]
    public async Task Handle_NoInvoiceVatAndNegativeCashContribution_ReturnsNegativeCollectedVatWithoutThrowing()
    {
        var cashVat = new List<CashSaleVatPosting> { new(19, -100.000m, -19.000m) };
        var handler = BuildHandler(cashVat, invoiceVat19: 0m, invoiceHt19: 0m);

        var result = await handler.Handle(new GetVatDeclarationQuery(2026, 7), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.CollectedVat19.Should().Be(-19.000m);
        result.Value.CollectedVat13.Should().Be(0m);
        result.Value.CollectedVat7.Should().Be(0m);

        var b19 = result.Value.CollectedVatBreakdown.Single(b => b.RatePercent == 19);
        b19.TaxableBase.Should().Be(-100.000m);
        b19.VatAmount.Should().Be(-19.000m);
    }

    [Fact]
    public async Task Handle_JournalRepositoryReturnsNull_DoesNotCrashAndContributesNothing()
    {
        var handler = BuildHandler(null);

        var result = await handler.Handle(new GetVatDeclarationQuery(2026, 7), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.CollectedVat19.Should().Be(190.000m);
        result.Value.CollectedVat13.Should().Be(0m);
        result.Value.CollectedVat7.Should().Be(0m);
    }
}
