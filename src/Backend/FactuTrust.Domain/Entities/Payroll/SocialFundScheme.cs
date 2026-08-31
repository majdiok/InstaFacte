using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities.Payroll;

/// <summary>Régime de caisse / mutuelle complémentaire configurable.</summary>
public sealed class SocialFundScheme : AggregateRoot
{
    public string Code { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public decimal EmployeeRatePercent { get; private set; }
    public decimal EmployerRatePercent { get; private set; }
    public SocialFundBase Base { get; private set; }
    public decimal FixedEmployeeAmount { get; private set; }
    public decimal FixedEmployerAmount { get; private set; }
    public decimal? MonthlyEmployeeCap { get; private set; }
    public string EmployeeAccountSce { get; private set; } = "428.1";
    /// <summary>Compte de dette SCE de la part employeur (fonds social). Default doctrinal « 4538 »
    /// (fonds social à payer) — cf. plan §4 WS-1 R-14 : « 647 » est un compte de charge, pas de dette.</summary>
    public string EmployerAccountSce { get; private set; } = "4538";
    public DateTime? EffectiveFrom { get; private set; }
    public DateTime? EffectiveTo { get; private set; }

    private SocialFundScheme() { }

    public static Result<SocialFundScheme> Create(
        string code,
        string name,
        decimal employeeRatePercent,
        decimal employerRatePercent,
        SocialFundBase fundBase,
        decimal fixedEmployeeAmount = 0,
        decimal fixedEmployerAmount = 0,
        decimal? monthlyEmployeeCap = null,
        string employeeAccountSce = "428.1",
        string employerAccountSce = "4538",
        DateTime? effectiveFrom = null,
        DateTime? effectiveTo = null)
    {
        if (string.IsNullOrWhiteSpace(code))
            return Result.Failure<SocialFundScheme>(Error.Validation("Code", "Le code est obligatoire."));
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure<SocialFundScheme>(Error.Validation("Name", "Le libellé est obligatoire."));
        if (employeeRatePercent < 0 || employerRatePercent < 0)
            return Result.Failure<SocialFundScheme>(Error.Validation("Rate", "Les taux doivent être positifs ou nuls."));

        return Result.Success(new SocialFundScheme
        {
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            IsActive = true,
            EmployeeRatePercent = employeeRatePercent,
            EmployerRatePercent = employerRatePercent,
            Base = fundBase,
            FixedEmployeeAmount = R(fixedEmployeeAmount),
            FixedEmployerAmount = R(fixedEmployerAmount),
            MonthlyEmployeeCap = monthlyEmployeeCap.HasValue ? R(monthlyEmployeeCap.Value) : null,
            EmployeeAccountSce = string.IsNullOrWhiteSpace(employeeAccountSce) ? "428.1" : employeeAccountSce.Trim(),
            EmployerAccountSce = string.IsNullOrWhiteSpace(employerAccountSce) ? "4538" : employerAccountSce.Trim(),
            EffectiveFrom = effectiveFrom?.Date,
            EffectiveTo = effectiveTo?.Date
        });
    }

    public Result Update(
        string name,
        bool isActive,
        decimal employeeRatePercent,
        decimal employerRatePercent,
        SocialFundBase fundBase,
        decimal fixedEmployeeAmount,
        decimal fixedEmployerAmount,
        decimal? monthlyEmployeeCap,
        string employeeAccountSce,
        string employerAccountSce,
        DateTime? effectiveFrom,
        DateTime? effectiveTo)
    {
        if (string.IsNullOrWhiteSpace(name))
            return Result.Failure(Error.Validation("Name", "Le libellé est obligatoire."));

        Name = name.Trim();
        IsActive = isActive;
        EmployeeRatePercent = employeeRatePercent;
        EmployerRatePercent = employerRatePercent;
        Base = fundBase;
        FixedEmployeeAmount = R(fixedEmployeeAmount);
        FixedEmployerAmount = R(fixedEmployerAmount);
        MonthlyEmployeeCap = monthlyEmployeeCap.HasValue ? R(monthlyEmployeeCap.Value) : null;
        EmployeeAccountSce = employeeAccountSce.Trim();
        EmployerAccountSce = employerAccountSce.Trim();
        EffectiveFrom = effectiveFrom?.Date;
        EffectiveTo = effectiveTo?.Date;
        IncrementVersion();
        return Result.Success();
    }

    public bool IsEffectiveOn(DateTime date)
    {
        if (!IsActive) return false;
        if (EffectiveFrom.HasValue && date.Date < EffectiveFrom.Value.Date) return false;
        if (EffectiveTo.HasValue && date.Date > EffectiveTo.Value.Date) return false;
        return true;
    }

    private static decimal R(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
}
