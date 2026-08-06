using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.Payroll.Declarations.CnssRemittance;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application.Payroll;

public sealed class CnssRemittanceExportTests
{
    private static PayrollYearParameters Params() => PayrollParameterDefaults.CreateDefaults(2026).Value;

    private static PayrollRun BuildValidatedRun(int year, int month, decimal baseSalary)
    {
        var input = new PayrollComputationInput
        {
            BaseSalary = baseSalary,
            Regime = SocialRegime.Rsna,
            WorkAccidentRate = 0.4m
        };
        var parameters = Params();
        var computation = PayrollCalculator.Compute(input, parameters);
        var (empRate, employerRate) = PayrollCalculator.ResolveCnssRates(input.Regime, parameters);
        var employeeId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var payslip = Payslip.FromComputation(
            Guid.NewGuid(), employeeId, "Alice Dupont", "EMP-001", "1234567890",
            year, month, computation, empRate, employerRate);
        var run = PayrollRun.Create(year, month, year).Value;
        run.SetPayslips([payslip]);
        run.Validate("test");
        return run;
    }

    private static CnssRemittanceDataLoader CreateLoader(PayrollRun? run)
    {
        var runs = new Mock<FactuTrust.Application.Common.Interfaces.Repositories.IPayrollRunRepository>();
        runs.Setup(r => r.GetByPeriodWithPayslipsAsync(2026, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(run);

        var payments = new Mock<FactuTrust.Application.Common.Interfaces.Repositories.ICnssContributionPaymentRepository>();
        payments.Setup(p => p.GetActiveByPeriodAsync(2026, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CnssContributionPayment?)null);

        var company = new Mock<FactuTrust.Application.Common.Interfaces.ITenantCompanySummaryProvider>();
        company.Setup(c => c.GetCurrentTenantSummaryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FactuTrust.Application.DTOs.TenantCompanySummaryDto
            {
                CompanyName = "Société Test",
                Nif = "1234567A",
                TaxRegimeDisplay = "Réel",
                CnssEmployerNumber = "9988776655"
            });

        return new CnssRemittanceDataLoader(runs.Object, payments.Object, company.Object);
    }

    [Fact]
    public async Task ExportCsv_FeatureDisabled_Fails()
    {
        var handler = new ExportCnssRemittanceCsvQueryHandler(
            CreateLoader(BuildValidatedRun(2026, 3, 2000m)),
            Options.Create(new AccountingSettings { PayrollCnssRemittanceEnabled = false }));

        var result = await handler.Handle(new ExportCnssRemittanceCsvQuery(2026, 3), CancellationToken.None);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task ExportCsv_ValidatedRun_ContainsTotals()
    {
        var run = BuildValidatedRun(2026, 3, 2000m);
        var handler = new ExportCnssRemittanceCsvQueryHandler(
            CreateLoader(run),
            Options.Create(new AccountingSettings { PayrollCnssRemittanceEnabled = true }));

        var result = await handler.Handle(new ExportCnssRemittanceCsvQuery(2026, 3), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var text = System.Text.Encoding.UTF8.GetString(result.Value);
        Assert.Contains("TOTAL À VERSER", text);
        Assert.Contains("9988776655", text);
        Assert.Contains("Alice Dupont", text);
    }
}
