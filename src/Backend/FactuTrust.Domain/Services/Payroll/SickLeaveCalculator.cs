namespace FactuTrust.Domain.Services.Payroll;

/// <summary>
/// Calcul des indemnités journalières CNSS et du maintien employeur pour un congé maladie.
/// Fonction pure — aucune valeur légale codée en dur : carence et taux IJ proviennent de l'appelant.
/// </summary>
public static class SickLeaveCalculator
{
    public const decimal MonthlyWorkingDays = 26m;
    public const int MaxIjDaysPerYear = 180;

    public static SickLeaveResult Compute(SickLeaveInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (input.SickDaysInMonth <= 0)
            return SickLeaveResult.Empty;

        var dailyRate = R(input.BaseSalary / MonthlyWorkingDays);
        var waitingPolicy = Math.Max(0, input.WaitingDaysPolicy);

        // Jours de carence restants sur cet épisode (avant le mois courant).
        var waitingAlreadyConsumed = Math.Min(input.PriorSickDaysInEpisode, waitingPolicy);
        var waitingRemaining = Math.Max(0, waitingPolicy - waitingAlreadyConsumed);

        var waitingDaysInMonth = Math.Min(input.SickDaysInMonth, waitingRemaining);
        var ijDaysInMonth = input.SickDaysInMonth - waitingDaysInMonth;

        // Plafond annuel IJ CNSS (180 jours).
        var ijDaysAlreadyPaid = Math.Min(input.PriorIjDaysInYear, MaxIjDaysPerYear);
        var ijDaysRemaining = Math.Max(0, MaxIjDaysPerYear - ijDaysAlreadyPaid);
        ijDaysInMonth = Math.Min(ijDaysInMonth, ijDaysRemaining);

        var ijRate = input.IjRatePercent / 100m;
        var cnssIjAmount = R(ijDaysInMonth * dailyRate * ijRate);
        var deductionAmount = R(waitingDaysInMonth * dailyRate);

        decimal topUpAmount = 0m;
        if (input.EmployerTopUpPercent.HasValue && ijDaysInMonth > 0)
        {
            var topUpDays = input.EmployerTopUpDays.HasValue
                ? Math.Min(ijDaysInMonth, input.EmployerTopUpDays.Value)
                : ijDaysInMonth;

            var targetRate = input.EmployerTopUpPercent.Value / 100m;
            var complementRate = Math.Max(0m, targetRate - ijRate);
            topUpAmount = R(topUpDays * dailyRate * complementRate);
        }

        var subrogationAdvance = input.SubrogationEnabled ? cnssIjAmount : 0m;

        return new SickLeaveResult
        {
            WaitingDays = waitingDaysInMonth,
            IjDays = ijDaysInMonth,
            DailyRate = dailyRate,
            DeductionAmount = deductionAmount,
            CnssIjAmount = cnssIjAmount,
            EmployerTopUpAmount = topUpAmount,
            SubrogationAdvanceAmount = subrogationAdvance
        };
    }

    private static decimal R(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
}

public sealed record SickLeaveInput
{
    public decimal SickDaysInMonth { get; init; }
    public decimal BaseSalary { get; init; }
    /// <summary>Jours maladie déjà consommés dans l'épisode avant le mois courant.</summary>
    public decimal PriorSickDaysInEpisode { get; init; }
    /// <summary>Jours indemnisés CNSS déjà consommés dans l'année civile.</summary>
    public decimal PriorIjDaysInYear { get; init; }
    public int WaitingDaysPolicy { get; init; }
    public decimal IjRatePercent { get; init; }
    public bool SubrogationEnabled { get; init; }
    public decimal? EmployerTopUpPercent { get; init; }
    public int? EmployerTopUpDays { get; init; }
}

public sealed class SickLeaveResult
{
    public static SickLeaveResult Empty { get; } = new();

    public decimal WaitingDays { get; init; }
    public decimal IjDays { get; init; }
    public decimal DailyRate { get; init; }
    /// <summary>Retenue sur brut pour les jours de carence.</summary>
    public decimal DeductionAmount { get; init; }
    /// <summary>Montant IJ CNSS théorique pour le mois.</summary>
    public decimal CnssIjAmount { get; init; }
    /// <summary>Complément employeur pour atteindre le taux de maintien cible.</summary>
    public decimal EmployerTopUpAmount { get; init; }
    /// <summary>Avance employeur en subrogation (créance CNSS).</summary>
    public decimal SubrogationAdvanceAmount { get; init; }
}
