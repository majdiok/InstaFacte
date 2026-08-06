namespace FactuTrust.Domain.Services.Payroll;

public enum PayrollProrataReason
{
    None = 0,
    Hire = 1,
    Departure = 2,
    Suspension = 3,
    Combined = 4
}

/// <summary>Période de suspension pour le calcul du prorata (données légères, sans dépendance infra).</summary>
public sealed record PayrollProrataSuspensionPeriod(
    DateTime StartDate,
    DateTime? EndDate,
    bool IsPaid,
    bool IsApproved);

public sealed class PayrollProrataMonthInput
{
    public int Year { get; init; }
    public int Month { get; init; }
    public decimal BaseSalary { get; init; }
    public DateTime EffectiveStart { get; init; }
    public DateTime EffectiveEnd { get; init; }
    public bool IsEnabled { get; init; }
    public IReadOnlyList<PayrollProrataSuspensionPeriod> Suspensions { get; init; } =
        Array.Empty<PayrollProrataSuspensionPeriod>();
}

public sealed class PayrollProrataMonthResult
{
    public decimal WorkedDays { get; init; }
    public decimal NonWorkedDays { get; init; }
    public decimal DeductionAmount { get; init; }
    public PayrollProrataReason Reason { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    public static PayrollProrataMonthResult Empty => new()
    {
        WorkedDays = PayrollWorkingDaysCounter.MonthlyWorkingDays,
        NonWorkedDays = 0m,
        DeductionAmount = 0m,
        Reason = PayrollProrataReason.None
    };
}
