using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.Payroll;

/// <summary>
/// Règle de prime annuelle paramétrable (13e mois, ancienneté, vacances…).
/// </summary>
public sealed class AnnualBonusRule : AggregateRoot
{
    public string Code { get; private set; } = null!;
    public string Label { get; private set; } = null!;
    public AnnualBonusKind Kind { get; private set; }
    public AnnualBonusFormula Formula { get; private set; }
    /// <summary>Mois de versement (1-12).</summary>
    public int PaymentMonth { get; private set; }
    public decimal FixedAmount { get; private set; }
    public decimal RatePercent { get; private set; }
    public decimal MonthsOfBase { get; private set; }
    public bool Taxable { get; private set; }
    public bool SubjectToCnss { get; private set; }
    public bool IsActive { get; private set; }
    public int? FiscalYear { get; private set; }

    private AnnualBonusRule() { }

    public static Result<AnnualBonusRule> Create(
        string code,
        string label,
        AnnualBonusKind kind,
        AnnualBonusFormula formula,
        int paymentMonth,
        decimal fixedAmount = 0m,
        decimal ratePercent = 0m,
        decimal monthsOfBase = 0m,
        bool taxable = true,
        bool subjectToCnss = true,
        int? fiscalYear = null)
    {
        if (string.IsNullOrWhiteSpace(code))
            return Result.Failure<AnnualBonusRule>(Error.Validation("Code", "Le code est obligatoire."));
        if (string.IsNullOrWhiteSpace(label))
            return Result.Failure<AnnualBonusRule>(Error.Validation("Label", "Le libellé est obligatoire."));
        if (paymentMonth is < 1 or > 12)
            return Result.Failure<AnnualBonusRule>(Error.Validation("PaymentMonth", "Le mois de versement doit être entre 1 et 12."));

        return Result.Success(new AnnualBonusRule
        {
            Code = code.Trim(),
            Label = label.Trim(),
            Kind = kind,
            Formula = formula,
            PaymentMonth = paymentMonth,
            FixedAmount = R(fixedAmount),
            RatePercent = R(ratePercent),
            MonthsOfBase = R(monthsOfBase),
            Taxable = taxable,
            SubjectToCnss = subjectToCnss,
            IsActive = true,
            FiscalYear = fiscalYear
        });
    }

    public Result Update(
        string label,
        AnnualBonusKind kind,
        AnnualBonusFormula formula,
        int paymentMonth,
        decimal fixedAmount,
        decimal ratePercent,
        decimal monthsOfBase,
        bool taxable,
        bool subjectToCnss,
        bool isActive,
        int? fiscalYear)
    {
        if (string.IsNullOrWhiteSpace(label))
            return Result.Failure(Error.Validation("Label", "Le libellé est obligatoire."));
        if (paymentMonth is < 1 or > 12)
            return Result.Failure(Error.Validation("PaymentMonth", "Le mois de versement doit être entre 1 et 12."));

        Label = label.Trim();
        Kind = kind;
        Formula = formula;
        PaymentMonth = paymentMonth;
        FixedAmount = R(fixedAmount);
        RatePercent = R(ratePercent);
        MonthsOfBase = R(monthsOfBase);
        Taxable = taxable;
        SubjectToCnss = subjectToCnss;
        IsActive = isActive;
        FiscalYear = fiscalYear;
        IncrementVersion();
        return Result.Success();
    }

    private static decimal R(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
}

/// <summary>
/// Affectation d'une règle de prime annuelle à un salarié (surcharge optionnelle du montant).
/// </summary>
public sealed class EmployeeAnnualBonusRule : AggregateRoot
{
    public Guid EmployeeId { get; private set; }
    public Guid AnnualBonusRuleId { get; private set; }
    public bool IsActive { get; private set; }
    public decimal? OverrideFixedAmount { get; private set; }
    public decimal? OverrideRatePercent { get; private set; }
    public decimal? OverrideMonthsOfBase { get; private set; }

    private EmployeeAnnualBonusRule() { }

    public static Result<EmployeeAnnualBonusRule> Create(
        Guid employeeId,
        Guid annualBonusRuleId,
        decimal? overrideFixedAmount = null,
        decimal? overrideRatePercent = null,
        decimal? overrideMonthsOfBase = null)
    {
        if (employeeId == Guid.Empty)
            return Result.Failure<EmployeeAnnualBonusRule>(Error.Validation("EmployeeId", "Le salarié est obligatoire."));
        if (annualBonusRuleId == Guid.Empty)
            return Result.Failure<EmployeeAnnualBonusRule>(Error.Validation("AnnualBonusRuleId", "La règle est obligatoire."));

        return Result.Success(new EmployeeAnnualBonusRule
        {
            EmployeeId = employeeId,
            AnnualBonusRuleId = annualBonusRuleId,
            IsActive = true,
            OverrideFixedAmount = overrideFixedAmount.HasValue ? R(overrideFixedAmount.Value) : null,
            OverrideRatePercent = overrideRatePercent.HasValue ? R(overrideRatePercent.Value) : null,
            OverrideMonthsOfBase = overrideMonthsOfBase.HasValue ? R(overrideMonthsOfBase.Value) : null
        });
    }

    public Result Update(
        bool isActive,
        decimal? overrideFixedAmount,
        decimal? overrideRatePercent,
        decimal? overrideMonthsOfBase)
    {
        IsActive = isActive;
        OverrideFixedAmount = overrideFixedAmount.HasValue ? R(overrideFixedAmount.Value) : null;
        OverrideRatePercent = overrideRatePercent.HasValue ? R(overrideRatePercent.Value) : null;
        OverrideMonthsOfBase = overrideMonthsOfBase.HasValue ? R(overrideMonthsOfBase.Value) : null;
        IncrementVersion();
        return Result.Success();
    }

    private static decimal R(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
}
