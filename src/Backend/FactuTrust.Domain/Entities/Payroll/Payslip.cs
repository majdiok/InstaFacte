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
    /// <summary>IRPP brut avant exonération/déduction SMIG.</summary>
    public decimal IrppBeforeSmigExemption { get; private set; }
    /// <summary>Montant de l'exonération IRPP SMIG appliquée.</summary>
    public decimal IrppSmigExemption { get; private set; }
    /// <summary>
    /// Forfait mensuel de la déduction annuelle SMIG (500 TND/an ÷ 12) appliqué sur la base
    /// imposable (mode <c>SmigAnnualDeduction</c>, R-12). 0 hors de ce mode. Figé pour audit.
    /// </summary>
    public decimal SmigAnnualDeductionAmount { get; private set; }
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

    /// <summary>
    /// R-22 : vrai si au moins une retenue n'a pas pu être prélevée intégralement faute de net
    /// suffisant et a été partiellement reportée au mois suivant. 0 sur les bulletins antérieurs.
    /// </summary>
    public bool HasPartialDeductions { get; private set; }
    /// <summary>R-22 : total des retenues reportées au mois suivant (pré- + post-impôt).</summary>
    public decimal PartialDeductionCarryOver { get; private set; }

    public decimal NetSalary { get; private set; }

    /// <summary>Jours travaillés (prorata, convention 26 jours). 0 si prorata inactif.</summary>
    public decimal ProrataWorkedDays { get; private set; }
    /// <summary>Jours non rémunérés (prorata). 0 si prorata inactif.</summary>
    public decimal ProrataNonWorkedDays { get; private set; }
    /// <summary>Montant de la retenue prorata sur le salaire de base.</summary>
    public decimal ProrataDeductionAmount { get; private set; }

    public decimal CnssEmployer { get; private set; }
    public decimal WorkAccidentContribution { get; private set; }
    public decimal Tfp { get; private set; }
    public decimal Foprolos { get; private set; }
    public decimal CssEmployer { get; private set; }

    /// <summary>
    /// Assiette des taxes sur salaires (TFP, FOPROLOS, CSS patronale) figée au calcul.
    /// <c>null</c> sur les bulletins antérieurs à son introduction.
    /// </summary>
    public decimal? PayrollTaxBase { get; private set; }

    // Instantané des principaux taux utilisés (pour traçabilité).
    public decimal AppliedCnssEmployeeRate { get; private set; }
    public decimal AppliedCnssEmployerRate { get; private set; }

    /// <summary>
    /// Taux de TFP appliqué (%), figé au calcul. Indispensable au formulaire officiel, qui
    /// distingue la ligne 1 % (industries manufacturières) de la ligne 2 % (autres activités) :
    /// le relire dans les paramètres exposerait à une modification de taux postérieure au cycle.
    /// <c>null</c> sur les bulletins antérieurs à son introduction.
    /// </summary>
    public decimal? AppliedTfpRate { get; private set; }

    /// <summary>Montant déjà payé (trésorerie).</summary>
    public decimal PaidAmount { get; private set; }
    /// <summary>Date du dernier paiement (ou date de solde complet).</summary>
    public DateTime? PaidAt { get; private set; }
    /// <summary>Compte auxiliaire 425XXXXXXX figé pour la comptabilité.</summary>
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
        decimal appliedCnssEmployerRate,
        decimal prorataWorkedDays = 0m,
        decimal prorataNonWorkedDays = 0m,
        decimal prorataDeductionAmount = 0m)
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
            SmigAnnualDeductionAmount = computation.SmigAnnualDeductionAmount,
            OtherDeductions = computation.OtherDeductions,
            NonTaxableAllowances = computation.NonTaxableAllowances,
            IrppRegularization = computation.IrppRegularization,
            CssRegularization = computation.CssRegularization,
            RegularizationDeferred = computation.RegularizationDeferred,
            HasPartialDeductions = computation.HasPartialDeductions,
            PartialDeductionCarryOver = computation.PartialDeductionCarryOver,
            NetSalary = computation.NetSalary,
            ProrataWorkedDays = prorataWorkedDays,
            ProrataNonWorkedDays = prorataNonWorkedDays,
            ProrataDeductionAmount = prorataDeductionAmount,
            CnssEmployer = computation.CnssEmployer,
            WorkAccidentContribution = computation.WorkAccidentContribution,
            Tfp = computation.Tfp,
            Foprolos = computation.Foprolos,
            CssEmployer = computation.CssEmployer,
            PayrollTaxBase = computation.PayrollTaxBase,
            AppliedCnssEmployeeRate = appliedCnssEmployeeRate,
            AppliedCnssEmployerRate = appliedCnssEmployerRate,
            AppliedTfpRate = computation.AppliedTfpRate
        };

        foreach (var line in computation.Lines)
        {
            payslip._lines.Add(PayslipLine.Create(
                payslip.Id, line.Order, line.Label, line.Kind, line.Base, line.Rate, line.Amount, line.DeductionKind,
                line.EarningKind, line.AccountSce, line.SourceEntityId, line.RequestedAmount, line.CarriedOverAmount));
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

    /// <summary>Figé le compte auxiliaire 425 pour la comptabilité (à la validation).</summary>
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
    /// <summary>Nature du gain (pour ventilation comptable SCE). Null pour les lignes historiques.</summary>
    public EarningKind? EarningKind { get; private set; }
    /// <summary>Compte SCE figé au calcul (ex. compte du régime de fonds social). Null si non applicable.</summary>
    public string? AccountSce { get; private set; }
    /// <summary>
    /// Identifiant de l'entité source (avance, prêt, saisie...) dont cette ligne est issue, figé au calcul.
    /// Sert à ne solder à la validation que les éléments effectivement reflétés dans ce bulletin.
    /// </summary>
    public Guid? SourceEntityId { get; private set; }
    /// <summary>Montant initialement demandé (utile pour les saisies partiellement retenues). Null si non applicable.</summary>
    public decimal? RequestedAmount { get; private set; }
    /// <summary>Solde reporté au mois suivant (saisies). Null si non applicable.</summary>
    public decimal? CarriedOverAmount { get; private set; }

    private PayslipLine() { }

    internal static PayslipLine Create(
        Guid payslipId, int order, string label, PayslipLineKind kind, decimal? baseAmount, decimal? rate, decimal amount,
        DeductionKind? deductionKind = null,
        EarningKind? earningKind = null,
        string? accountSce = null,
        Guid? sourceEntityId = null,
        decimal? requestedAmount = null,
        decimal? carriedOverAmount = null)
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
            DeductionKind = deductionKind,
            EarningKind = earningKind,
            AccountSce = accountSce,
            SourceEntityId = sourceEntityId,
            RequestedAmount = requestedAmount,
            CarriedOverAmount = carriedOverAmount
        };
    }
}
