using FactuTrust.Application.Common;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.Queries;
using FactuTrust.Application.Features.Reports.Queries;
using FactuTrust.Application.Features.WithholdingTax.Queries;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application;

public sealed class GetVatDeclarationV2Tests
{
    private static Mock<ITenantCompanySummaryProvider> CreateTenantSummaryMock(
        TenantCompanySummaryDto? summary = null)
    {
        var mock = new Mock<ITenantCompanySummaryProvider>();
        mock.Setup(p => p.GetCurrentTenantSummaryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(summary ?? new TenantCompanySummaryDto
            {
                CompanyName = "Ma Société SARL",
                Nif = "1234567/A/B/C/000",
                TaxRegimeDisplay = "Régime réel",
                TradeName = "Commerce"
            });
        return mock;
    }

    private static Mock<IPayrollRunRepository> CreatePayrollRunMock()
    {
        var mock = new Mock<IPayrollRunRepository>();
        mock.Setup(r => r.GetByPeriodAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PayrollRun?)null);
        return mock;
    }

    [Fact]
    public async Task Handle_V2_AutoComputesFodecTclTimbreAndRs()
    {
        var mediator = new Mock<IMediator>();
        var salesRows = new List<SalesVatReportRowDto>
        {
            new() { VatRatePercent = 19, VatRateDisplay = "19 %", TotalVatAmount = 190m, TotalTaxableAmount = 1000m, Currency = "TND" }
        };
        mediator.Setup(m => m.Send(It.IsAny<GetSalesVatReportQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<SalesVatReportRowDto>>(salesRows));
        mediator.Setup(m => m.Send(It.IsAny<GetPurchasesVatReportQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<PurchasesVatReportRowDto>>(new List<PurchasesVatReportRowDto>()));
        mediator.Setup(m => m.Send(It.IsAny<GetWithholdingMonthlyReportQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WithholdingMonthlyReportDto(2026, 7, new(), 0m, 40m, 0));

        var vatRepo = new Mock<IVatDeclarationRepository>();
        vatRepo.Setup(r => r.GetByYearMonthAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VatDeclaration?)null);

        var journals = new Mock<IJournalEntryRepository>();
        journals.Setup(r => r.SumDebitsByAccountAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        var invoices = new Mock<IInvoiceRepository>();
        invoices.Setup(r => r.SumFiscalStampAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(12m);
        invoices.Setup(r => r.SumFodecAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(10m);
        invoices.Setup(r => r.SumFodecTaxableBaseAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1000m);

        var supplierInvoices = new Mock<ISupplierInvoiceRepository>();
        supplierInvoices.Setup(r => r.SumFixedAssetDeductibleVatAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        var settings = Options.Create(new AccountingSettings
        {
            MonthlyDeclarationV2Enabled = true,
            FodecRatePercent = 1.0m,
            TclRatePercent = 0.2m
        });

        var tenantSummary = CreateTenantSummaryMock();
        var handler = new GetVatDeclarationQueryHandler(
            mediator.Object, vatRepo.Object, journals.Object, invoices.Object, supplierInvoices.Object, tenantSummary.Object, CreatePayrollRunMock().Object, settings);

        var result = await handler.Handle(new GetVatDeclarationQuery(2026, 7), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var dto = result.Value;
        Assert.Equal(10m, dto.Fodec);
        Assert.Equal(1000m, dto.FodecTaxableBase);
        Assert.Equal(2.38m, dto.Tcl);
        Assert.Equal(12m, dto.DroitTimbre);
        Assert.Equal(40m, dto.WithholdingTax);
        Assert.True(dto.MonthlyDeclarationV2Enabled);
        Assert.Equal(254.38m, dto.TotalToPay);
        Assert.Equal(1000m, dto.SalesTaxableBase);
        Assert.Equal(1190m, dto.SalesGrossBase);
        // Taux configurés exposés pour l'affichage / la valeur suggérée à l'écran.
        Assert.Equal(1.0m, dto.FodecRatePercent);
        Assert.Equal(0.2m, dto.TclRatePercent);
        // Assiette légale : la déclaration ne consomme que les factures éligibles (hors brouillon/annulée).
        mediator.Verify(m => m.Send(It.Is<GetSalesVatReportQuery>(q => q.RealizedOnly), It.IsAny<CancellationToken>()), Times.Once);
        mediator.Verify(m => m.Send(It.Is<GetPurchasesVatReportQuery>(q => q.RealizedOnly), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(new DateTime(2026, 8, 22), dto.FilingDeadline);
        Assert.Equal("Déclaration mensuelle unique", dto.DeclarationTypeDisplay);
        Assert.Equal(3, dto.CollectedVatBreakdown.Count);
        Assert.Equal(1000m, dto.CollectedVatBreakdown.First(r => r.RatePercent == 19).TaxableBase);
        Assert.Equal(190m, dto.CollectedVatBreakdown.First(r => r.RatePercent == 19).VatAmount);
        Assert.Equal("Ma Société SARL", dto.CompanyName);
        Assert.Equal("1234567/A/B/C/000", dto.Nif);
        Assert.Equal("Régime réel", dto.TaxRegimeDisplay);
    }

    [Fact]
    public async Task Handle_DeductibleAssets_DerivedFromSupplierInvoiceLines_NotJournal()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetSalesVatReportQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<SalesVatReportRowDto>>(new List<SalesVatReportRowDto>()));
        mediator.Setup(m => m.Send(It.IsAny<GetPurchasesVatReportQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<PurchasesVatReportRowDto>>(new List<PurchasesVatReportRowDto>
            {
                new() { VatRatePercent = 19, VatRateDisplay = "19 %", TotalVatAmount = 300m, TotalTaxableAmount = 2000m, Currency = "TND" }
            }));
        mediator.Setup(m => m.Send(It.IsAny<GetWithholdingMonthlyReportQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WithholdingMonthlyReportDto(2026, 7, new(), 0m, 0m, 0));

        var vatRepo = new Mock<IVatDeclarationRepository>();
        vatRepo.Setup(r => r.GetByYearMonthAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VatDeclaration?)null);

        var journals = new Mock<IJournalEntryRepository>();
        journals.Setup(r => r.SumDebitsByAccountAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(999m); // ne doit PAS être utilisé quand des lignes immobilisation existent

        var invoices = new Mock<IInvoiceRepository>();
        var supplierInvoices = new Mock<ISupplierInvoiceRepository>();
        supplierInvoices.Setup(r => r.SumFixedAssetDeductibleVatAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(120m);

        var settings = Options.Create(new AccountingSettings { MonthlyDeclarationV2Enabled = true });
        var tenantSummary = CreateTenantSummaryMock();

        var handler = new GetVatDeclarationQueryHandler(
            mediator.Object, vatRepo.Object, journals.Object, invoices.Object, supplierInvoices.Object, tenantSummary.Object, CreatePayrollRunMock().Object, settings);

        var result = await handler.Handle(new GetVatDeclarationQuery(2026, 7), CancellationToken.None);

        Assert.True(result.IsSuccess);
        // TVA immobilisations issue des lignes de facture fournisseur, biens = total − immobilisations.
        Assert.Equal(120m, result.Value.DeductibleVatAssets);
        Assert.Equal(180m, result.Value.DeductibleVatGoods);
        journals.Verify(r => r.SumDebitsByAccountAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DeductibleAssets_FallsBackToJournal_WhenNoFixedAssetLines()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetSalesVatReportQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<SalesVatReportRowDto>>(new List<SalesVatReportRowDto>()));
        mediator.Setup(m => m.Send(It.IsAny<GetPurchasesVatReportQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<PurchasesVatReportRowDto>>(new List<PurchasesVatReportRowDto>
            {
                new() { VatRatePercent = 19, VatRateDisplay = "19 %", TotalVatAmount = 300m, TotalTaxableAmount = 2000m, Currency = "TND" }
            }));
        mediator.Setup(m => m.Send(It.IsAny<GetWithholdingMonthlyReportQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WithholdingMonthlyReportDto(2026, 7, new(), 0m, 0m, 0));

        var vatRepo = new Mock<IVatDeclarationRepository>();
        vatRepo.Setup(r => r.GetByYearMonthAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VatDeclaration?)null);

        var journals = new Mock<IJournalEntryRepository>();
        journals.Setup(r => r.SumDebitsByAccountAsync("43662", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(50m);

        var invoices = new Mock<IInvoiceRepository>();
        var supplierInvoices = new Mock<ISupplierInvoiceRepository>();
        supplierInvoices.Setup(r => r.SumFixedAssetDeductibleVatAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m); // aucune ligne immobilisation → repli journal

        var settings = Options.Create(new AccountingSettings { MonthlyDeclarationV2Enabled = true });
        var tenantSummary = CreateTenantSummaryMock();

        var handler = new GetVatDeclarationQueryHandler(
            mediator.Object, vatRepo.Object, journals.Object, invoices.Object, supplierInvoices.Object, tenantSummary.Object, CreatePayrollRunMock().Object, settings);

        var result = await handler.Handle(new GetVatDeclarationQuery(2026, 7), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(50m, result.Value.DeductibleVatAssets);
        Assert.Equal(250m, result.Value.DeductibleVatGoods);
    }

    [Fact]
    public async Task Handle_SavedDeclaration_IncludesMetadata()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetSalesVatReportQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<SalesVatReportRowDto>>(new List<SalesVatReportRowDto>()));
        mediator.Setup(m => m.Send(It.IsAny<GetPurchasesVatReportQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<PurchasesVatReportRowDto>>(new List<PurchasesVatReportRowDto>()));

        var createdAt = new DateTime(2024, 6, 1, 10, 30, 0, DateTimeKind.Utc);
        var saved = VatDeclaration.CreateDraft(
            2024, 5,
            Money.Create(100m, "TND"), Money.Create(0m, "TND"), Money.Create(0m, "TND"),
            Money.Create(0m, "TND"), Money.Create(0m, "TND"), Money.Create(0m, "TND"));
        saved.GetType().GetProperty("CreatedAt")!.SetValue(saved, createdAt);
        saved.GetType().GetProperty("CreatedBy")!.SetValue(saved, "admin@test.com");

        var vatRepo = new Mock<IVatDeclarationRepository>();
        vatRepo.Setup(r => r.GetByYearMonthAsync(2024, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(saved);
        vatRepo.Setup(r => r.GetByYearMonthAsync(2024, 4, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VatDeclaration?)null);

        var journals = new Mock<IJournalEntryRepository>();
        journals.Setup(r => r.SumDebitsByAccountAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);
        var invoices = new Mock<IInvoiceRepository>();
        var supplierInvoices = new Mock<ISupplierInvoiceRepository>();
        supplierInvoices.Setup(r => r.SumFixedAssetDeductibleVatAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);
        var settings = Options.Create(new AccountingSettings { MonthlyDeclarationV2Enabled = true });
        var tenantSummary = CreateTenantSummaryMock();

        var handler = new GetVatDeclarationQueryHandler(
            mediator.Object, vatRepo.Object, journals.Object, invoices.Object, supplierInvoices.Object, tenantSummary.Object, CreatePayrollRunMock().Object, settings);
        var result = await handler.Handle(new GetVatDeclarationQuery(2024, 5), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(createdAt, result.Value.CreatedAt);
        Assert.Equal("admin@test.com", result.Value.CreatedBy);
        Assert.Equal(new DateTime(2024, 6, 22), result.Value.FilingDeadline);
    }

    [Fact]
    public void VatFilingDeadline_ForPeriod_May2024_ReturnsJune22()
    {
        Assert.Equal(new DateTime(2024, 6, 22), VatFilingDeadline.ForPeriod(2024, 5));
    }

    [Fact]
    public async Task Handle_V2Disabled_LeavesOtherTaxesZero()
    {
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<GetSalesVatReportQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<SalesVatReportRowDto>>(new List<SalesVatReportRowDto>
            {
                new() { VatRatePercent = 19, VatRateDisplay = "19 %", TotalVatAmount = 190m, TotalTaxableAmount = 1000m, Currency = "TND" }
            }));
        mediator.Setup(m => m.Send(It.IsAny<GetPurchasesVatReportQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<PurchasesVatReportRowDto>>(new List<PurchasesVatReportRowDto>()));

        var vatRepo = new Mock<IVatDeclarationRepository>();
        vatRepo.Setup(r => r.GetByYearMonthAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((VatDeclaration?)null);
        var journals = new Mock<IJournalEntryRepository>();
        journals.Setup(r => r.SumDebitsByAccountAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);
        var invoices = new Mock<IInvoiceRepository>();
        var supplierInvoices = new Mock<ISupplierInvoiceRepository>();
        supplierInvoices.Setup(r => r.SumFixedAssetDeductibleVatAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);
        var settings = Options.Create(new AccountingSettings { MonthlyDeclarationV2Enabled = false });
        var tenantSummary = CreateTenantSummaryMock();

        var handler = new GetVatDeclarationQueryHandler(
            mediator.Object, vatRepo.Object, journals.Object, invoices.Object, supplierInvoices.Object, tenantSummary.Object, CreatePayrollRunMock().Object, settings);

        var result = await handler.Handle(new GetVatDeclarationQuery(2026, 7), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0m, result.Value.Fodec);
        Assert.Equal(0m, result.Value.DroitTimbre);
        Assert.Equal(190m, result.Value.TotalToPay);
        invoices.Verify(r => r.SumFiscalStampAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
