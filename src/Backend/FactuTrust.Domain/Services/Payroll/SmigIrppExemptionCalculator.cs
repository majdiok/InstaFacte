using FactuTrust.Domain.Entities.Payroll;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Services.Payroll;

/// <summary>
/// Exonération IRPP SMIG (article 21) — logique partagée entre le calcul mensuel et la régularisation annuelle.
/// </summary>
public static class SmigIrppExemptionCalculator
{
    /// <summary>
    /// Applique l'exonération IRPP SMIG sur un IRPP mensuel déjà calculé au barème progressif.
    /// </summary>
    public static SmigIrppExemptionResult ApplyMonthly(
        decimal irppBeforeExemption,
        decimal monthlyNetTaxable,
        decimal baseSalary,
        PayrollYearParameters parameters)
    {
        if (parameters.SmigIrppExemptionMode == SmigIrppExemptionMode.None || irppBeforeExemption <= 0)
        {
            return new SmigIrppExemptionResult(irppBeforeExemption, 0m);
        }

        return parameters.SmigIrppExemptionMode switch
        {
            SmigIrppExemptionMode.SmigPortion => ApplySmigPortion(
                irppBeforeExemption,
                monthlyNetTaxable,
                parameters.MonthlySmig,
                parameters.ResolveSmigExemptionRate()),
            SmigIrppExemptionMode.FullIfBelow when baseSalary <= parameters.MonthlySmig =>
                new SmigIrppExemptionResult(0m, irppBeforeExemption),
            _ => new SmigIrppExemptionResult(irppBeforeExemption, 0m)
        };
    }

    /// <summary>
    /// Applique l'exonération IRPP SMIG sur l'IRPP dû au cumul annuel (régularisation).
    /// </summary>
    public static SmigIrppExemptionResult ApplyOnCumul(
        decimal irppDueBeforeExemption,
        decimal cumulNetTaxable,
        IReadOnlyList<IrppRegularizationMonth> months,
        PayrollYearParameters parameters)
    {
        if (parameters.SmigIrppExemptionMode == SmigIrppExemptionMode.None || irppDueBeforeExemption <= 0)
        {
            return new SmigIrppExemptionResult(irppDueBeforeExemption, 0m);
        }

        return parameters.SmigIrppExemptionMode switch
        {
            SmigIrppExemptionMode.SmigPortion => ApplySmigPortion(
                irppDueBeforeExemption,
                cumulNetTaxable,
                parameters.MonthlySmig * months.Count,
                parameters.ResolveSmigExemptionRate()),
            SmigIrppExemptionMode.FullIfBelow when months.Count > 0
                && months.All(m => m.BaseSalary <= parameters.MonthlySmig) =>
                new SmigIrppExemptionResult(0m, irppDueBeforeExemption),
            _ => new SmigIrppExemptionResult(irppDueBeforeExemption, 0m)
        };
    }

    private static SmigIrppExemptionResult ApplySmigPortion(
        decimal irppBeforeExemption,
        decimal taxableBase,
        decimal smigCap,
        decimal ratePercent)
    {
        var exemptedBase = Math.Min(taxableBase, smigCap);
        var exemption = R(exemptedBase * ratePercent / 100m);
        var irppFinal = Math.Max(0m, R(irppBeforeExemption - exemption));
        return new SmigIrppExemptionResult(irppFinal, R(irppBeforeExemption - irppFinal));
    }

    private static decimal R(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
}

/// <summary>Résultat d'une exonération IRPP SMIG.</summary>
public readonly record struct SmigIrppExemptionResult(decimal IrppFinal, decimal ExemptionAmount);
