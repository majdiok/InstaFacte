using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Events;
using FactuTrust.Domain.Services.Payroll;

namespace FactuTrust.Domain.Entities.Payroll;

/// <summary>
/// Cycle de paie mensuel : regroupe les bulletins d'un mois et pilote leur workflow
/// (brouillon → calculé → validé → clôturé).
/// </summary>
public sealed class PayrollRun : AggregateRoot
{
    public int Year { get; private set; }
    public int Month { get; private set; }
    public string Label { get; private set; } = null!;
    public PayrollRunStatus Status { get; private set; }
    /// <summary>Exercice des paramètres légaux utilisés pour ce cycle.</summary>
    public int ParametersFiscalYear { get; private set; }

    public DateTime? CalculatedAt { get; private set; }
    public DateTime? ValidatedAt { get; private set; }
    public string? ValidatedBy { get; private set; }
    public DateTime? ClosedAt { get; private set; }

    // Totaux (figés au calcul).
    public decimal TotalGross { get; private set; }
    public decimal TotalCnssEmployee { get; private set; }
    public decimal TotalIrpp { get; private set; }
    public decimal TotalCss { get; private set; }
    public decimal TotalNet { get; private set; }
    /// <summary>
    /// Somme des nets imposables mensuels (<see cref="Payslip.MonthlyNetTaxable"/>). Assiette
    /// des articles 1 et 3 du formulaire officiel (IRPP et CSS salariale). Distincte de
    /// <see cref="TotalGross"/> et de <see cref="TotalPayrollTaxBase"/>.
    /// </summary>
    public decimal TotalNetTaxable { get; private set; }
    public decimal TotalCnssEmployer { get; private set; }
    public decimal TotalTfp { get; private set; }
    public decimal TotalFoprolos { get; private set; }
    /// <summary>
    /// Assiette cumulée des taxes sur salaires (TFP, FOPROLOS, CSS patronale). Reportée telle
    /// quelle sur le formulaire officiel de la déclaration mensuelle. <c>null</c> sur les cycles
    /// antérieurs à son introduction : la déclaration retombe alors sur une reconstitution.
    /// </summary>
    public decimal? TotalPayrollTaxBase { get; private set; }
    /// <summary>Taux de TFP appliqué par le cycle (%). <c>null</c> sur les cycles antérieurs.</summary>
    public decimal? AppliedTfpRate { get; private set; }
    public decimal TotalCssEmployer { get; private set; }
    public decimal TotalWorkAccident { get; private set; }
    /// <summary>Somme des autres retenues (avances, oppositions) figées au calcul.</summary>
    public decimal TotalOtherDeductions { get; private set; }
    /// <summary>
    /// Somme des régularisations IRPP annuelles (signée : positive si les rappels l'emportent,
    /// négative si les restitutions dominent). Tenue à part de <see cref="TotalIrpp"/>, qui
    /// conserve le sens d'IRPP mensuel du cycle.
    /// </summary>
    public decimal TotalIrppRegularization { get; private set; }
    /// <summary>Somme des régularisations CSS annuelles, même convention de signe.</summary>
    public decimal TotalCssRegularization { get; private set; }
    /// <summary>Somme des exonérations/déductions IRPP SMIG appliquées sur le cycle.</summary>
    public decimal TotalIrppSmigExemption { get; private set; }

    private readonly List<Payslip> _payslips = new();
    public IReadOnlyCollection<Payslip> Payslips => _payslips.AsReadOnly();

    public decimal TotalPaid => R(_payslips.Sum(p => p.PaidAmount));
    public decimal RemainingToPay => R(TotalNet - TotalPaid);
    public bool HasPayments => _payslips.Any(p => p.PaidAmount > 0);

    public PayrollRunPaymentStatus PaymentStatus
    {
        get
        {
            if (!HasPayments)
                return PayrollRunPaymentStatus.NotPaid;
            if (RemainingToPay <= 0)
                return PayrollRunPaymentStatus.FullyPaid;
            return PayrollRunPaymentStatus.PartiallyPaid;
        }
    }

    private PayrollRun() { }

    public static Result<PayrollRun> Create(int year, int month, int parametersFiscalYear, string? label = null)
    {
        if (year is < 2000 or > 2100)
            return Result.Failure<PayrollRun>(Error.Validation("Year", "L'année doit être comprise entre 2000 et 2100."));
        if (month is < 1 or > 12)
            return Result.Failure<PayrollRun>(Error.Validation("Month", "Le mois doit être compris entre 1 et 12."));
        if (parametersFiscalYear is < 2000 or > 2100)
            return Result.Failure<PayrollRun>(Error.Validation("ParametersFiscalYear", "L'exercice des paramètres est invalide."));

        return Result.Success(new PayrollRun
        {
            Year = year,
            Month = month,
            ParametersFiscalYear = parametersFiscalYear,
            Label = string.IsNullOrWhiteSpace(label) ? $"Paie {month:D2}/{year}" : label.Trim(),
            Status = PayrollRunStatus.Draft
        });
    }

    /// <summary>Remplace tous les bulletins et recalcule les totaux. Interdit si le cycle n'est plus modifiable.</summary>
    public Result SetPayslips(IEnumerable<Payslip> payslips)
    {
        if (!Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Un cycle validé ou clôturé ne peut plus être recalculé."));

        _payslips.Clear();
        _payslips.AddRange(payslips);
        RecomputeTotals();
        Status = PayrollRunStatus.Calculated;
        CalculatedAt = DateTime.UtcNow;
        IncrementVersion();
        return Result.Success();
    }

    public Result Validate(string validatedBy)
    {
        if (Status != PayrollRunStatus.Calculated)
            return Result.Failure(Error.Validation("Status", "Seul un cycle calculé peut être validé."));
        if (_payslips.Count == 0)
            return Result.Failure(Error.Validation("Payslips", "Impossible de valider un cycle sans bulletin."));

        Status = PayrollRunStatus.Validated;
        ValidatedAt = DateTime.UtcNow;
        ValidatedBy = string.IsNullOrWhiteSpace(validatedBy) ? null : validatedBy.Trim();
        IncrementVersion();
        AddDomainEvent(new PayrollRunValidatedEvent(Id, Year, Month));
        return Result.Success();
    }

    /// <summary>
    /// R-15 : fige le compte auxiliaire 425 de chaque bulletin à la validation (et non plus
    /// paresseusement au paiement/OD). Le compte devient déterministe et traçable : un changement
    /// de matricule entre validation et paiement n'a plus d'effet. Renvoie un échec nominatif si un
    /// matricule ne contient aucun chiffre (compte SCE strictement numérique). En cas de succès,
    /// retourne la liste des bulletins dont le compte vient d'être figé (à persister).
    /// </summary>
    public Result<IReadOnlyList<Payslip>> FreezeEmployeeAuxiliaryAccounts()
    {
        var frozen = new List<Payslip>();
        foreach (var payslip in _payslips)
        {
            if (!string.IsNullOrWhiteSpace(payslip.EmployeeAuxiliaryAccount))
                continue; // déjà figé (cycle recalculé conserve le compte existant)

            if (!PayrollEmployeeAuxiliaryAccountResolver.CanResolve(payslip.EmployeeNumber))
            {
                return Result.Failure<IReadOnlyList<Payslip>>(Error.Validation(
                    "EmployeeNumber",
                    $"Le matricule « {payslip.EmployeeNumber} » du salarié {payslip.EmployeeName} ne contient "
                    + "aucun chiffre : impossible de générer le compte auxiliaire 425 (SCE strictement numérique)."));
            }

            payslip.EnsureAuxiliaryAccount(PayrollEmployeeAuxiliaryAccountResolver.Resolve(payslip.EmployeeNumber));
            frozen.Add(payslip);
        }

        return Result.Success<IReadOnlyList<Payslip>>(frozen);
    }

    public Result Close()
    {
        if (Status != PayrollRunStatus.Validated)
            return Result.Failure(Error.Validation("Status", "Seul un cycle validé peut être clôturé."));

        Status = PayrollRunStatus.Closed;
        ClosedAt = DateTime.UtcNow;
        IncrementVersion();
        AddDomainEvent(new PayrollRunClosedEvent(Id, Year, Month));
        return Result.Success();
    }

    /// <summary>Rouvre un cycle validé (avant clôture) pour correction.</summary>
    public Result Reopen()
    {
        if (Status != PayrollRunStatus.Validated)
            return Result.Failure(Error.Validation("Status", "Seul un cycle validé (non clôturé) peut être rouvert."));

        if (HasPayments)
        {
            return Result.Failure(Error.Validation(
                "Payments",
                "Impossible de rouvrir : des paiements existent. Annulez d'abord tous les paiements."));
        }

        Status = PayrollRunStatus.Calculated;
        ValidatedAt = null;
        ValidatedBy = null;
        IncrementVersion();
        return Result.Success();
    }

    private void RecomputeTotals()
    {
        TotalGross = R(_payslips.Sum(p => p.GrossSalary));
        TotalCnssEmployee = R(_payslips.Sum(p => p.CnssEmployee));
        TotalIrpp = R(_payslips.Sum(p => p.Irpp));
        TotalCss = R(_payslips.Sum(p => p.Css));
        TotalNet = R(_payslips.Sum(p => p.NetSalary));
        TotalNetTaxable = R(_payslips.Sum(p => p.MonthlyNetTaxable));
        TotalCnssEmployer = R(_payslips.Sum(p => p.CnssEmployer));
        TotalTfp = R(_payslips.Sum(p => p.Tfp));
        TotalFoprolos = R(_payslips.Sum(p => p.Foprolos));
        // Assiette et taux : null tant qu'aucun bulletin ne les porte (cycles recalculés depuis
        // des bulletins antérieurs à leur introduction). Le taux est commun à tout le cycle.
        TotalPayrollTaxBase = _payslips.Any(p => p.PayrollTaxBase.HasValue)
            ? R(_payslips.Sum(p => p.PayrollTaxBase ?? 0m))
            : null;
        AppliedTfpRate = _payslips
            .Select(p => p.AppliedTfpRate)
            .FirstOrDefault(rate => rate.HasValue);
        TotalCssEmployer = R(_payslips.Sum(p => p.CssEmployer));
        TotalWorkAccident = R(_payslips.Sum(p => p.WorkAccidentContribution));
        TotalOtherDeductions = R(_payslips.Sum(p => p.OtherDeductions));
        TotalIrppRegularization = R(_payslips.Sum(p => p.IrppRegularization));
        TotalCssRegularization = R(_payslips.Sum(p => p.CssRegularization));
        TotalIrppSmigExemption = R(_payslips.Sum(p => p.IrppSmigExemption));
    }

    private static decimal R(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
}
