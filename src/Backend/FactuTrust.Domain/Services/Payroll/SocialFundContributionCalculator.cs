using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Services.Payroll;

/// <summary>Calcule les cotisations salariales et patronales d'une caisse / mutuelle.</summary>
public static class SocialFundContributionCalculator
{
    public sealed record ContributionResult(
        decimal EmployeeAmount,
        decimal EmployerAmount,
        string EmployeeAccountSce,
        string EmployerAccountSce);

    public static ContributionResult Compute(
        SocialFundScheme scheme,
        EmployeeSocialFundEnrollment enrollment,
        decimal grossCnssable,
        decimal monthlyNetTaxable)
    {
        var employeeBase = ResolveBase(scheme.Base, grossCnssable, monthlyNetTaxable, scheme.FixedEmployeeAmount);
        var employerBase = ResolveBase(scheme.Base, grossCnssable, monthlyNetTaxable, scheme.FixedEmployerAmount);

        var employeeAmount = enrollment.OverrideEmployeeAmount
            ?? (scheme.Base == SocialFundBase.FixedAmount
                ? scheme.FixedEmployeeAmount
                : R(employeeBase * scheme.EmployeeRatePercent / 100m));

        if (scheme.MonthlyEmployeeCap.HasValue)
            employeeAmount = Math.Min(employeeAmount, scheme.MonthlyEmployeeCap.Value);

        var employerAmount = enrollment.OverrideEmployerAmount
            ?? (scheme.Base == SocialFundBase.FixedAmount
                ? scheme.FixedEmployerAmount
                : R(employerBase * scheme.EmployerRatePercent / 100m));

        return new ContributionResult(
            R(employeeAmount),
            R(employerAmount),
            scheme.EmployeeAccountSce,
            scheme.EmployerAccountSce);
    }

    private static decimal ResolveBase(SocialFundBase fundBase, decimal grossCnssable, decimal monthlyNetTaxable, decimal fixedAmount) =>
        fundBase switch
        {
            SocialFundBase.GrossCnssable => grossCnssable,
            SocialFundBase.NetTaxable => monthlyNetTaxable,
            SocialFundBase.FixedAmount => fixedAmount,
            _ => grossCnssable
        };

    private static decimal R(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
}
