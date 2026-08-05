using FactuTrust.Domain.Banking;
using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;
using FactuTrust.Domain.Services.Payroll.BankTransfer;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Domain.Payroll;

public sealed class PayrollBankTransferBuilderTests
{
    private static readonly Guid Emp1 = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
    private static readonly Guid Emp2 = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222");
    private static readonly Guid Emp3 = Guid.Parse("cccccccc-3333-3333-3333-333333333333");

    private static PayrollYearParameters Params() => PayrollParameterDefaults.CreateDefaults(2026).Value;

    private static Payslip MakePayslip(PayrollRun run, Guid employeeId, string name, string number, decimal baseSalary)
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
        return Payslip.FromComputation(
            run.Id, employeeId, name, number, "1234567890",
            run.Year, run.Month, computation, empRate, employerRate);
    }

    private static Employee MakeEmployee(
        Guid id,
        string number,
        string first,
        string last,
        string? rib)
    {
        var emp = Employee.Create(number, first, last, new DateTime(2020, 1, 1), rib: rib).Value;
        // Force Id via reflection is not available; use Create then replace dictionary key.
        // Employee.Id is set by AggregateRoot on create — we map by returned Id.
        // For tests we need known IDs: set via private setter pattern — AggregateRoot generates Guid.
        // Workaround: use the created Id and pass matching payslip EmployeeId.
        typeof(FactuTrust.Domain.Common.Entity)
            .GetProperty(nameof(Employee.Id))!
            .SetValue(emp, id);
        return emp;
    }

    private static (PayrollRun Run, List<Payslip> Payslips, Dictionary<Guid, Employee> Employees) BuildValidatedRun()
    {
        var run = PayrollRun.Create(2026, 8, 2026).Value;
        var e1 = MakeEmployee(Emp1, "EMP001", "Ahmed", "Ben Ali", "20001234567890123456");
        var e2 = MakeEmployee(Emp2, "EMP002", "Sara", "Trabelsi", "08001234567890123456");
        var e3 = MakeEmployee(Emp3, "EMP003", "Karim", "SansRib", null);

        var p1 = MakePayslip(run, Emp1, e1.FullName, e1.EmployeeNumber, 2000m);
        var p2 = MakePayslip(run, Emp2, e2.FullName, e2.EmployeeNumber, 1500m);
        var p3 = MakePayslip(run, Emp3, e3.FullName, e3.EmployeeNumber, 1000m);

        run.SetPayslips([p1, p2, p3]);
        run.Validate("tester");

        return (run, [p1, p2, p3], new Dictionary<Guid, Employee>
        {
            [Emp1] = e1,
            [Emp2] = e2,
            [Emp3] = e3
        });
    }

    [Fact]
    public void Build_ThreePayslips_TwoEligible_OneExcludedMissingRib()
    {
        var (run, payslips, employees) = BuildValidatedRun();
        var options = new BankTransferExportOptions { TransferLabel = "Paie 08/2026" };

        var batch = PayrollBankTransferBuilder.Build(run, payslips, employees, options, null, null);

        Assert.Equal(2, batch.EligibleCount);
        Assert.Single(batch.ExcludedLines);
        Assert.Equal(PayrollBankTransferExclusionReason.MissingRib, batch.ExcludedLines[0].Reason);
        Assert.Equal(
            Math.Round(payslips[0].NetSalary + payslips[1].NetSalary, 3, MidpointRounding.AwayFromZero),
            batch.TotalAmount);
        Assert.Contains(batch.Warnings, w => w.Code == PayrollBankTransferWarningCode.NoDebtorAccount);
        Assert.Contains(batch.Warnings, w => w.Code == PayrollBankTransferWarningCode.EmployeesExcluded);
    }

    [Fact]
    public void Build_ZeroNet_Excluded()
    {
        var run = PayrollRun.Create(2026, 8, 2026).Value;
        var emp = MakeEmployee(Emp1, "EMP001", "Zero", "Net", "20001234567890123456");
        // Force net 0 by using base salary that still produces net > 0 is hard; use SivpExonere with 0 base.
        var input = new PayrollComputationInput
        {
            BaseSalary = 0m,
            Regime = SocialRegime.SivpExonere,
            WorkAccidentRate = 0m
        };
        var parameters = Params();
        var computation = PayrollCalculator.Compute(input, parameters);
        Assert.Equal(0m, computation.NetSalary);
        var payslip = Payslip.FromComputation(
            run.Id, Emp1, emp.FullName, emp.EmployeeNumber, null,
            2026, 8, computation, 0m, 0m);
        run.SetPayslips([payslip]);
        run.Validate("tester");

        var batch = PayrollBankTransferBuilder.Build(
            run, [payslip], new Dictionary<Guid, Employee> { [Emp1] = emp },
            new BankTransferExportOptions { TransferLabel = "Paie 08/2026" },
            null, null);

        Assert.Equal(0, batch.EligibleCount);
        Assert.Single(batch.ExcludedLines);
        Assert.Equal(PayrollBankTransferExclusionReason.ZeroOrNegativeNet, batch.ExcludedLines[0].Reason);
    }

    [Fact]
    public void Build_InvalidRib_Excluded()
    {
        var run = PayrollRun.Create(2026, 8, 2026).Value;
        var emp = MakeEmployee(Emp1, "EMP001", "Bad", "Rib", "12345"); // too short
        var payslip = MakePayslip(run, Emp1, emp.FullName, emp.EmployeeNumber, 2000m);
        run.SetPayslips([payslip]);
        run.Validate("tester");

        var batch = PayrollBankTransferBuilder.Build(
            run, [payslip], new Dictionary<Guid, Employee> { [Emp1] = emp },
            new BankTransferExportOptions { TransferLabel = "Paie 08/2026" },
            null, null);

        Assert.Equal(0, batch.EligibleCount);
        Assert.Equal(PayrollBankTransferExclusionReason.InvalidRib, batch.ExcludedLines[0].Reason);
    }

    [Fact]
    public void Build_CustomLabel_Propagated()
    {
        var (run, payslips, employees) = BuildValidatedRun();
        var batch = PayrollBankTransferBuilder.Build(
            run, payslips, employees,
            new BankTransferExportOptions { TransferLabel = "Salaire août 2026" },
            new BankTransferCompanyInfo("ACME SARL"),
            new BankTransferDebtorAccountInfo("08009999888877776666", "TN5908009999888877776666", "BIAT", "BIAT"));

        Assert.All(batch.Lines, l => Assert.Equal("Salaire août 2026", l.TransferLabel));
        Assert.Equal("ACME SARL", batch.Company!.Name);
        Assert.Equal("BIAT", batch.DebtorAccount!.BankName);
        Assert.DoesNotContain(batch.Warnings, w => w.Code == PayrollBankTransferWarningCode.NoDebtorAccount);
    }

    [Fact]
    public void Build_DerivesIbanFromRib()
    {
        var (run, payslips, employees) = BuildValidatedRun();
        var batch = PayrollBankTransferBuilder.Build(
            run, payslips, employees,
            new BankTransferExportOptions { TransferLabel = "Paie 08/2026" },
            null, null);

        var line = batch.Lines.First(l => l.EmployeeId == Emp1);
        Assert.Equal(20, line.Rib.Length);
        Assert.Equal(TunisianIban.FromRib(line.Rib), line.Iban);
        Assert.StartsWith("TN", line.Iban);
        Assert.Equal(24, line.Iban.Length);
    }

    [Fact]
    public void CanExport_OnlyValidatedOrClosed()
    {
        Assert.False(PayrollBankTransferBuilder.CanExport(PayrollRunStatus.Draft));
        Assert.False(PayrollBankTransferBuilder.CanExport(PayrollRunStatus.Calculated));
        Assert.True(PayrollBankTransferBuilder.CanExport(PayrollRunStatus.Validated));
        Assert.True(PayrollBankTransferBuilder.CanExport(PayrollRunStatus.Closed));
    }
}
