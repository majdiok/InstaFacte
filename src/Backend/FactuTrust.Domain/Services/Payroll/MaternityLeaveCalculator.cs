namespace FactuTrust.Domain.Services.Payroll;

/// <summary>
/// Calcul du maintien de salaire et de l'IJ CNSS pour un congé maternité.
/// </summary>
public static class MaternityLeaveCalculator
{
    public static MaternityLeaveResult Compute(MaternityLeaveInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.MaternityDaysInMonth <= 0)
            return MaternityLeaveResult.Empty;

        var dailyRate = R(input.BaseSalary / SickLeaveCalculator.MonthlyWorkingDays);
        var ijRate = input.IjRatePercent / 100m;
        var topUpRate = input.EmployerTopUpPercent / 100m;

        var cnssIjAmount = R(input.MaternityDaysInMonth * dailyRate * ijRate);
        var complementRate = Math.Max(0m, topUpRate - ijRate);
        var employerTopUpAmount = R(input.MaternityDaysInMonth * dailyRate * complementRate);

        return new MaternityLeaveResult
        {
            MaternityDays = input.MaternityDaysInMonth,
            DailyRate = dailyRate,
            CnssIjAmount = cnssIjAmount,
            EmployerTopUpAmount = employerTopUpAmount,
            TotalMaintenanceAmount = R(cnssIjAmount + employerTopUpAmount)
        };
    }

    private static decimal R(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
}

public sealed record MaternityLeaveInput
{
    public decimal MaternityDaysInMonth { get; init; }
    public decimal BaseSalary { get; init; }
    public decimal IjRatePercent { get; init; }
    public decimal EmployerTopUpPercent { get; init; }
}

public sealed class MaternityLeaveResult
{
    public static MaternityLeaveResult Empty { get; } = new();

    public decimal MaternityDays { get; init; }
    public decimal DailyRate { get; init; }
    public decimal CnssIjAmount { get; init; }
    public decimal EmployerTopUpAmount { get; init; }
    public decimal TotalMaintenanceAmount { get; init; }
}
