namespace FactuTrust.Domain.Services.Payroll;

/// <summary>
/// Calcul du maintien de salaire pour un congé paternité (jours ouvrables rémunérés).
/// </summary>
public static class PaternityLeaveCalculator
{
    public static PaternityLeaveResult Compute(PaternityLeaveInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.PaternityDaysInMonth <= 0)
            return PaternityLeaveResult.Empty;

        var dailyRate = R(input.BaseSalary / SickLeaveCalculator.MonthlyWorkingDays);
        var maintenanceAmount = R(input.PaternityDaysInMonth * dailyRate);

        return new PaternityLeaveResult
        {
            PaternityDays = input.PaternityDaysInMonth,
            DailyRate = dailyRate,
            MaintenanceAmount = maintenanceAmount
        };
    }

    private static decimal R(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
}

public sealed record PaternityLeaveInput
{
    public decimal PaternityDaysInMonth { get; init; }
    public decimal BaseSalary { get; init; }
}

public sealed class PaternityLeaveResult
{
    public static PaternityLeaveResult Empty { get; } = new();

    public decimal PaternityDays { get; init; }
    public decimal DailyRate { get; init; }
    /// <summary>Montant de maintien intégral pour les jours de paternité.</summary>
    public decimal MaintenanceAmount { get; init; }
}
