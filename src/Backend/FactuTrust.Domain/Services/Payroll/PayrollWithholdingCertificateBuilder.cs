using FactuTrust.Domain.Entities.Payroll;

namespace FactuTrust.Domain.Services.Payroll;

/// <summary>
/// Agrège les bulletins figés d'un exercice en certificats de retenue à la source (IRPP + CSS).
/// Fonction pure : seuls les cycles Validés/Clôturés doivent alimenter les payslips en entrée.
/// </summary>
public static class PayrollWithholdingCertificateBuilder
{
    private static readonly string[] MonthLabelsFr =
    {
        "", "Janvier", "Février", "Mars", "Avril", "Mai", "Juin",
        "Juillet", "Août", "Septembre", "Octobre", "Novembre", "Décembre"
    };

    public static PayrollWithholdingCertificateBatch Build(
        int year,
        IReadOnlyList<Payslip> payslips,
        IReadOnlyDictionary<Guid, EmployeeIdentitySnapshot> employees,
        EmployerSnapshot employer,
        IReadOnlyList<int> includedMonths)
    {
        ArgumentNullException.ThrowIfNull(payslips);
        ArgumentNullException.ThrowIfNull(employees);
        ArgumentNullException.ThrowIfNull(employer);
        ArgumentNullException.ThrowIfNull(includedMonths);

        var expectedMonths = Enumerable.Range(1, 12).ToArray();
        var distinctIncluded = includedMonths.Distinct().OrderBy(m => m).ToList();
        var missingMonths = expectedMonths.Except(distinctIncluded).ToList();
        var isComplete = missingMonths.Count == 0;

        var byEmployee = payslips
            .GroupBy(p => p.EmployeeId)
            .Select(g => BuildLine(g.Key, g.ToList(), employees, distinctIncluded, isComplete))
            .OrderBy(l => l.EmployeeName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new PayrollWithholdingCertificateBatch
        {
            Year = year,
            EmployerCompanyName = employer.CompanyName,
            EmployerNif = employer.Nif,
            EmployerAddressLine = employer.AddressLine,
            EmployeeCount = byEmployee.Count,
            IncludedMonths = distinctIncluded,
            MissingMonths = missingMonths,
            IsComplete = isComplete,
            TotalGross = R(byEmployee.Sum(l => l.TotalGross)),
            TotalAnnualNetTaxable = R(byEmployee.Sum(l => l.AnnualNetTaxable)),
            TotalIrppWithheld = R(byEmployee.Sum(l => l.TotalIrppWithheld)),
            TotalCssWithheld = R(byEmployee.Sum(l => l.TotalCssWithheld)),
            TotalWithholding = R(byEmployee.Sum(l => l.TotalWithholding)),
            Lines = byEmployee
        };
    }

    private static PayrollWithholdingCertificateLine BuildLine(
        Guid employeeId,
        IReadOnlyList<Payslip> employeePayslips,
        IReadOnlyDictionary<Guid, EmployeeIdentitySnapshot> employees,
        IReadOnlyList<int> includedMonths,
        bool isYearComplete)
    {
        var first = employeePayslips.OrderBy(p => p.Month).First();
        employees.TryGetValue(employeeId, out var identity);

        var monthsCount = employeePayslips.Select(p => p.Month).Distinct().Count();
        var isPartialYear = monthsCount < 12 || !isYearComplete;

        var warnings = new List<string>();
        var cin = identity?.Cin;
        var cnss = identity?.CnssNumber ?? first.CnssNumber;
        if (string.IsNullOrWhiteSpace(cin))
            warnings.Add("CIN manquant");
        if (string.IsNullOrWhiteSpace(cnss))
            warnings.Add("CNSS manquant");
        if (isPartialYear)
            warnings.Add("Année incomplète");

        var monthDetails = BuildMonthDetails(employeePayslips);
        var totalIrppWithheld = R(employeePayslips.Sum(p => p.Irpp + p.IrppRegularization));
        var totalCssWithheld = R(employeePayslips.Sum(p => p.Css + p.CssRegularization));

        return new PayrollWithholdingCertificateLine
        {
            EmployeeId = employeeId,
            EmployeeNumber = identity?.EmployeeNumber ?? first.EmployeeNumber,
            EmployeeName = identity?.EmployeeName ?? first.EmployeeName,
            Cin = cin,
            CnssNumber = cnss,
            AddressLine = identity?.AddressLine,
            IsHeadOfFamily = identity?.IsHeadOfFamily ?? false,
            MonthsCount = monthsCount,
            IsPartialYear = isPartialYear,
            TotalGross = R(employeePayslips.Sum(p => p.GrossSalary)),
            TotalCnssableGross = R(employeePayslips.Sum(p => p.CnssableGross)),
            TotalCnssEmployee = R(employeePayslips.Sum(p => p.CnssEmployee)),
            TotalProfessionalExpenses = R(employeePayslips.Sum(p => p.ProfessionalExpenses)),
            TotalFamilyDeductions = R(employeePayslips.Sum(p => p.FamilyDeductions)),
            AnnualNetTaxable = R(employeePayslips.Sum(p => p.MonthlyNetTaxable)),
            TotalIrppWithheld = totalIrppWithheld,
            TotalCssWithheld = totalCssWithheld,
            TotalWithholding = R(totalIrppWithheld + totalCssWithheld),
            TotalIrppSmigExemption = R(employeePayslips.Sum(p => p.IrppSmigExemption)),
            Warnings = warnings,
            Months = monthDetails
        };
    }

    private static IReadOnlyList<PayrollWithholdingCertificateMonth> BuildMonthDetails(
        IReadOnlyList<Payslip> employeePayslips)
    {
        var byMonth = employeePayslips.ToDictionary(p => p.Month);
        var result = new List<PayrollWithholdingCertificateMonth>();

        for (var m = 1; m <= 12; m++)
        {
            if (!byMonth.TryGetValue(m, out var payslip))
            {
                result.Add(new PayrollWithholdingCertificateMonth(
                    m,
                    MonthLabelsFr[m],
                    0m,
                    0m,
                    0m,
                    0m,
                    0m,
                    HasPayslip: false));
                continue;
            }

            result.Add(new PayrollWithholdingCertificateMonth(
                m,
                MonthLabelsFr[m],
                payslip.MonthlyNetTaxable,
                payslip.Irpp,
                payslip.IrppRegularization,
                payslip.Css,
                payslip.CssRegularization,
                HasPayslip: true));
        }

        return result;
    }

    private static decimal R(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
}
