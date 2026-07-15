using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services.Payroll;

namespace FactuTrust.Domain.Entities.Payroll;

/// <summary>
/// Bulletin de paie d'un salarié pour un mois : instantané figé du calcul et des lignes.
/// </summary>
public sealed class Payslip : Entity
{
    public Guid PayrollRunId { get; private set; }
    public Guid EmployeeId { get; private set; }
    /// <summary>Nom du salarié figé au moment du calcul.</summary>
    public string EmployeeName { get; private set; } = null!;
    public string EmployeeNumber { get; private set; } = null!;
    public string? CnssNumber { get; private set; }

    public int Year { get; private set; }
    public int Month { get; private set; }

    public decimal GrossSalary { get; private set; }
    public decimal CnssableGross { get; private set; }
    public decimal CnssEmployee { get; private set; }
    public decimal TaxableBaseAfterCnss { get; private set; }
    public decimal ProfessionalExpenses { get; private set; }
    public decimal FamilyDeductions { get; private set; }
    public decimal MonthlyNetTaxable { get; private set; }
    public decimal AnnualNetTaxable { get; private set; }
    public decimal Irpp { get; private set; }
    public decimal Css { get; private set; }
    public decimal OtherDeductions { get; private set; }
    public decimal NonTaxableAllowances { get; private set; }
    public decimal NetSalary { get; private set; }

    public decimal CnssEmployer { get; private set; }
    public decimal WorkAccidentContribution { get; private set; }
    public decimal Tfp { get; private set; }
    public decimal Foprolos { get; private set; }

    // Instantané des principaux taux utilisés (pour traçabilité).
    public decimal AppliedCnssEmployeeRate { get; private set; }
    public decimal AppliedCnssEmployerRate { get; private set; }

    private readonly List<PayslipLine> _lines = new();
    public IReadOnlyCollection<PayslipLine> Lines => _lines.AsReadOnly();

    private Payslip() { }

    /// <summary>
    /// Construit un bulletin figé à partir du résultat du moteur de calcul.
    /// </summary>
    public static Payslip FromComputation(
        Guid payrollRunId,
        Guid employeeId,
        string employeeName,
        string employeeNumber,
        string? cnssNumber,
        int year,
        int month,
        PayrollComputation computation,
        decimal appliedCnssEmployeeRate,
        decimal appliedCnssEmployerRate)
    {
        var payslip = new Payslip
        {
            PayrollRunId = payrollRunId,
            EmployeeId = employeeId,
            EmployeeName = employeeName,
            EmployeeNumber = employeeNumber,
            CnssNumber = cnssNumber,
            Year = year,
            Month = month,
            GrossSalary = computation.GrossSalary,
            CnssableGross = computation.CnssableGross,
            CnssEmployee = computation.CnssEmployee,
            TaxableBaseAfterCnss = computation.TaxableBaseAfterCnss,
            ProfessionalExpenses = computation.ProfessionalExpenses,
            FamilyDeductions = computation.FamilyDeductions,
            MonthlyNetTaxable = computation.MonthlyNetTaxable,
            AnnualNetTaxable = computation.AnnualNetTaxable,
            Irpp = computation.Irpp,
            Css = computation.Css,
            OtherDeductions = computation.OtherDeductions,
            NonTaxableAllowances = computation.NonTaxableAllowances,
            NetSalary = computation.NetSalary,
            CnssEmployer = computation.CnssEmployer,
            WorkAccidentContribution = computation.WorkAccidentContribution,
            Tfp = computation.Tfp,
            Foprolos = computation.Foprolos,
            AppliedCnssEmployeeRate = appliedCnssEmployeeRate,
            AppliedCnssEmployerRate = appliedCnssEmployerRate
        };

        foreach (var line in computation.Lines)
        {
            payslip._lines.Add(PayslipLine.Create(
                payslip.Id, line.Order, line.Label, line.Kind, line.Base, line.Rate, line.Amount));
        }

        return payslip;
    }
}

/// <summary>
/// Ligne d'un bulletin de paie (gain, retenue, charge patronale, information).
/// </summary>
public sealed class PayslipLine : Entity
{
    public Guid PayslipId { get; private set; }
    public int Order { get; private set; }
    public string Label { get; private set; } = null!;
    public PayslipLineKind Kind { get; private set; }
    public decimal? Base { get; private set; }
    public decimal? Rate { get; private set; }
    public decimal Amount { get; private set; }

    private PayslipLine() { }

    internal static PayslipLine Create(
        Guid payslipId, int order, string label, PayslipLineKind kind, decimal? baseAmount, decimal? rate, decimal amount)
    {
        return new PayslipLine
        {
            PayslipId = payslipId,
            Order = order,
            Label = label,
            Kind = kind,
            Base = baseAmount,
            Rate = rate,
            Amount = Math.Round(amount, 3, MidpointRounding.AwayFromZero)
        };
    }
}
