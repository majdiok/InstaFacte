using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Events;

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
    public decimal TotalCnssEmployer { get; private set; }
    public decimal TotalTfp { get; private set; }
    public decimal TotalFoprolos { get; private set; }
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
    /// <summary>Somme des exonérations IRPP SMIG (art. 21) appliquées sur le cycle.</summary>
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
        TotalCnssEmployer = R(_payslips.Sum(p => p.CnssEmployer));
        TotalTfp = R(_payslips.Sum(p => p.Tfp));
        TotalFoprolos = R(_payslips.Sum(p => p.Foprolos));
        TotalCssEmployer = R(_payslips.Sum(p => p.CssEmployer));
        TotalWorkAccident = R(_payslips.Sum(p => p.WorkAccidentContribution));
        TotalOtherDeductions = R(_payslips.Sum(p => p.OtherDeductions));
        TotalIrppRegularization = R(_payslips.Sum(p => p.IrppRegularization));
        TotalCssRegularization = R(_payslips.Sum(p => p.CssRegularization));
        TotalIrppSmigExemption = R(_payslips.Sum(p => p.IrppSmigExemption));
    }

    private static decimal R(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
}
