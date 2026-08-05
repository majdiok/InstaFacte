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
    /// <summary>IRPP brut avant exonération SMIG (art. 21).</summary>
    public decimal IrppBeforeSmigExemption { get; private set; }
    /// <summary>Montant de l'exonération IRPP SMIG appliquée.</summary>
    public decimal IrppSmigExemption { get; private set; }
    public decimal OtherDeductions { get; private set; }
    public decimal NonTaxableAllowances { get; private set; }

    /// <summary>
    /// Régularisation IRPP annuelle portée sur ce bulletin (signée : positive pour un rappel,
    /// négative pour une restitution). <see cref="Irpp"/> conserve l'IRPP mensuel pur — cette
    /// séparation rend le recalcul des cumuls idempotent.
    /// </summary>
    public decimal IrppRegularization { get; private set; }
    /// <summary>Régularisation CSS annuelle, même convention de signe.</summary>
    public decimal CssRegularization { get; private set; }
    /// <summary>Part du rappel non prélevée faute de net suffisant (0 si aucun écrêtage).</summary>
    public decimal RegularizationDeferred { get; private set; }

    public decimal NetSalary { get; private set; }

    public decimal CnssEmployer { get; private set; }
    public decimal WorkAccidentContribution { get; private set; }
    public decimal Tfp { get; private set; }
    public decimal Foprolos { get; private set; }

    // Instantané des principaux taux utilisés (pour traçabilité).
    public decimal AppliedCnssEmployeeRate { get; private set; }
    public decimal AppliedCnssEmployerRate { get; private set; }

    /// <summary>Montant déjà payé (trésorerie).</summary>
    public decimal PaidAmount { get; private set; }
    /// <summary>Date du dernier paiement (ou date de solde complet).</summary>
    public DateTime? PaidAt { get; private set; }
    /// <summary>Compte auxiliaire 421xxxx figé pour la comptabilité.</summary>
    public string? EmployeeAuxiliaryAccount { get; private set; }

    public decimal RemainingToPay => R(NetSalary - PaidAmount);

    public PayslipPaymentStatus PaymentStatus
    {
        get
        {
            if (PaidAmount <= 0)
                return PayslipPaymentStatus.Unpaid;
            if (RemainingToPay <= 0)
                return PayslipPaymentStatus.Paid;
            return PayslipPaymentStatus.PartiallyPaid;
        }
    }

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
            IrppBeforeSmigExemption = computation.IrppBeforeSmigExemption,
            IrppSmigExemption = computation.IrppSmigExemption,
            OtherDeductions = computation.OtherDeductions,
            NonTaxableAllowances = computation.NonTaxableAllowances,
            IrppRegularization = computation.IrppRegularization,
            CssRegularization = computation.CssRegularization,
            RegularizationDeferred = computation.RegularizationDeferred,
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
                payslip.Id, line.Order, line.Label, line.Kind, line.Base, line.Rate, line.Amount, line.DeductionKind));
        }

        return payslip;
    }

    /// <summary>Enregistre un paiement sur le bulletin.</summary>
    public Result RegisterPayment(decimal amount, DateTime paymentDate, string? auxiliaryAccount = null)
    {
        if (amount <= 0)
            return Result.Failure(Error.Validation("Amount", "Le montant doit être positif."));

        if (amount > RemainingToPay + 0.001m)
        {
            return Result.Failure(Error.Validation(
                "Amount",
                $"Le montant ({amount:N3}) dépasse le reste à payer ({RemainingToPay:N3})."));
        }

        if (!string.IsNullOrWhiteSpace(auxiliaryAccount))
            EmployeeAuxiliaryAccount ??= auxiliaryAccount.Trim();

        PaidAmount = R(PaidAmount + amount);
        PaidAt = PaymentStatus == PayslipPaymentStatus.Paid ? paymentDate.Date : PaidAt ?? paymentDate.Date;
        if (PaymentStatus == PayslipPaymentStatus.Paid)
            PaidAt = paymentDate.Date;

        return Result.Success();
    }

    /// <summary>Figé le compte auxiliaire 421 pour la comptabilité (à la validation).</summary>
    internal void EnsureAuxiliaryAccount(string auxiliaryAccount)
    {
        if (!string.IsNullOrWhiteSpace(auxiliaryAccount))
            EmployeeAuxiliaryAccount ??= auxiliaryAccount.Trim();
    }

    /// <summary>Annule un paiement précédemment enregistré.</summary>
    public Result UnregisterPayment(decimal amount)
    {
        if (amount <= 0)
            return Result.Failure(Error.Validation("Amount", "Le montant doit être positif."));

        if (amount > PaidAmount + 0.001m)
        {
            return Result.Failure(Error.Validation(
                "Amount",
                "Le montant à annuler dépasse le total payé sur ce bulletin."));
        }

        PaidAmount = R(PaidAmount - amount);
        if (PaidAmount <= 0)
        {
            PaidAmount = 0;
            PaidAt = null;
        }

        return Result.Success();
    }

    private static decimal R(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
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
    /// <summary>Type de retenue (pour ventilation comptable). Null pour les lignes historiques.</summary>
    public DeductionKind? DeductionKind { get; private set; }

    private PayslipLine() { }

    internal static PayslipLine Create(
        Guid payslipId, int order, string label, PayslipLineKind kind, decimal? baseAmount, decimal? rate, decimal amount,
        DeductionKind? deductionKind = null)
    {
        return new PayslipLine
        {
            PayslipId = payslipId,
            Order = order,
            Label = label,
            Kind = kind,
            Base = baseAmount,
            Rate = rate,
            Amount = Math.Round(amount, 3, MidpointRounding.AwayFromZero),
            DeductionKind = deductionKind
        };
    }
}
