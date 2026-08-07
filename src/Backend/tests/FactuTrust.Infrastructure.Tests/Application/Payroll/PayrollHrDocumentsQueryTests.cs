using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Payroll.HrDocuments;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using FactuTrust.Infrastructure.Services;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Application.Payroll;

public sealed class PayrollHrDocumentsQueryTests
{
    private static readonly Guid EmployeeId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    private static PayrollYearParameters Params() => PayrollParameterDefaults.CreateDefaults(2026).Value;

    private static Employee BuildEmployee(bool terminated = false)
    {
        var employee = Employee.Create(
            "EMP-001",
            "Alice",
            "Dupont",
            new DateTime(2024, 1, 15),
            cin: "12345678",
            cnssNumber: "9876543210").Value;

        var contractResult = employee.AddContract(
            ContractType.Cdi,
            SocialRegime.Rsna,
            new DateTime(2024, 1, 15),
            2500m,
            0.4m,
            jobTitle: "Comptable");
        Assert.True(contractResult.IsSuccess);

        if (terminated)
            employee.Terminate(new DateTime(2026, 3, 31));

        return employee;
    }

    private static Payslip BuildPayslip(Guid payrollRunId, Guid employeeId, int year, int month, decimal baseSalary)
    {
        var input = new PayrollComputationInput
        {
            BaseSalary = baseSalary,
            Regime = SocialRegime.Rsna,
            WorkAccidentRate = 0.4m
        };
        var computation = PayrollCalculator.Compute(input, Params());
        var (empRate, employerRate) = PayrollCalculator.ResolveCnssRates(input.Regime, Params());
        return Payslip.FromComputation(
            payrollRunId,
            employeeId,
            "Alice Dupont",
            "EMP-001",
            "9876543210",
            year,
            month,
            computation,
            empRate,
            employerRate);
    }

    private static PayrollHrDocumentDataLoader CreateLoader(
        bool featureEnabled,
        Employee? employee = null,
        IReadOnlyList<Payslip>? payslips = null,
        TenantCompanySummaryDto? company = null)
    {
        var settings = Options.Create(new AccountingSettings { PayrollHrDocumentsEnabled = featureEnabled });

        var employeeRepo = new Mock<IEmployeeRepository>();
        employeeRepo.Setup(r => r.GetByIdWithContractsAsync(EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(employee);

        var runRepo = new Mock<IPayrollRunRepository>();
        runRepo.Setup(r => r.ListSettledPayslipsForEmployeeAsync(EmployeeId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payslips ?? Array.Empty<Payslip>());

        var companyProvider = new Mock<ITenantCompanySummaryProvider>();
        companyProvider.Setup(c => c.GetCurrentTenantSummaryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(company ?? new TenantCompanySummaryDto
            {
                CompanyName = "Société Test",
                Nif = "1234567A",
                TaxRegimeDisplay = "Réel",
                AddressLine = "Tunis"
            });

        return new PayrollHrDocumentDataLoader(settings, runRepo.Object, employeeRepo.Object, companyProvider.Object);
    }

    [Fact]
    public void EnsureFeatureEnabled_WhenDisabled_Fails()
    {
        var loader = CreateLoader(featureEnabled: false);
        var result = loader.EnsureFeatureEnabled();
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task EmploymentCertificate_EmployeeNotFound_Fails()
    {
        var loader = CreateLoader(featureEnabled: true, employee: null);
        var result = await loader.BuildEmploymentCertificateAsync(EmployeeId, CancellationToken.None);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task EmploymentCertificate_ActiveEmployee_Succeeds()
    {
        var employee = BuildEmployee(terminated: false);
        var loader = CreateLoader(featureEnabled: true, employee: employee);

        var result = await loader.BuildEmploymentCertificateAsync(EmployeeId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("EMP-001", result.Value.EmployeeNumber);
        Assert.True(result.Value.IsStillEmployed);
        Assert.Equal("Comptable", result.Value.JobTitle);
    }

    [Fact]
    public async Task SalaryCertificate_InvalidMonths_Fails()
    {
        var loader = CreateLoader(featureEnabled: true, employee: BuildEmployee());
        var result = await loader.BuildSalaryCertificateAsync(EmployeeId, 5, CancellationToken.None);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task SalaryCertificate_NoPayslips_Fails()
    {
        var loader = CreateLoader(featureEnabled: true, employee: BuildEmployee(), payslips: Array.Empty<Payslip>());
        var result = await loader.BuildSalaryCertificateAsync(EmployeeId, 3, CancellationToken.None);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task SalaryCertificate_ThreeMonths_ComputesAverages()
    {
        var runId = Guid.NewGuid();
        var payslips = new[]
        {
            BuildPayslip(runId, EmployeeId, 2026, 1, 2000m),
            BuildPayslip(runId, EmployeeId, 2026, 2, 2200m),
            BuildPayslip(runId, EmployeeId, 2026, 3, 2400m)
        };
        var loader = CreateLoader(featureEnabled: true, employee: BuildEmployee(), payslips: payslips);

        var result = await loader.BuildSalaryCertificateAsync(EmployeeId, 3, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.PayslipCount);
        Assert.Equal(3, result.Value.Months.Count);
        Assert.Equal(payslips.Sum(p => p.GrossSalary), result.Value.TotalGrossSalary);
        Assert.Equal(Math.Round(payslips.Average(p => p.NetSalary), 3, MidpointRounding.AwayFromZero), result.Value.AverageNetSalary);
    }

    [Fact]
    public async Task SoldeToutCompte_ActiveEmployee_Fails()
    {
        var loader = CreateLoader(featureEnabled: true, employee: BuildEmployee(terminated: false));
        var result = await loader.BuildSoldeToutCompteAsync(EmployeeId, CancellationToken.None);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task SoldeToutCompte_TerminatedWithoutPayslip_Fails()
    {
        var loader = CreateLoader(featureEnabled: true, employee: BuildEmployee(terminated: true), payslips: Array.Empty<Payslip>());
        var result = await loader.BuildSoldeToutCompteAsync(EmployeeId, CancellationToken.None);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task SoldeToutCompte_TerminatedWithSettlementPayslip_Succeeds()
    {
        var runId = Guid.NewGuid();
        var payslip = BuildPayslip(runId, EmployeeId, 2026, 3, 2500m);
        var loader = CreateLoader(
            featureEnabled: true,
            employee: BuildEmployee(terminated: true),
            payslips: new[] { payslip });

        var result = await loader.BuildSoldeToutCompteAsync(EmployeeId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2026, result.Value.SettlementYear);
        Assert.Equal(3, result.Value.SettlementMonth);
        Assert.Equal(payslip.NetSalary, result.Value.NetSalary);
    }

    [Fact]
    public async Task GenerateEmploymentCertificatePdfAsync_ReturnsNonEmptyBytes()
    {
        var service = new PdfService(new Mock<System.Net.Http.IHttpClientFactory>().Object, new Mock<IDocumentTemplateRegistry>().Object);
        var dto = new EmploymentCertificateDto
        {
            EmployerCompanyName = "Société Test",
            EmployerNif = "1234567A",
            EmployeeId = EmployeeId,
            EmployeeNumber = "EMP-001",
            EmployeeName = "Alice Dupont",
            Cin = "12345678",
            HireDate = new DateTime(2024, 1, 15),
            IsStillEmployed = true,
            JobTitle = "Comptable",
            ContractTypeDisplay = "CDI",
            ContractStartDate = new DateTime(2024, 1, 15),
            DocumentReference = "CT-EMP-001",
            GeneratedAt = DateTime.Now
        };

        var bytes = await service.GenerateEmploymentCertificatePdfAsync(dto, CancellationToken.None);
        Assert.NotEmpty(bytes);
    }
}
