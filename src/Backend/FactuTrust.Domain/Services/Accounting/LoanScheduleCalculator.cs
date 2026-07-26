using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Services.Accounting;

/// <summary>Une échéance calculée d'un emprunt (aucune persistance — résultat pur du calcul).</summary>
public sealed record LoanInstallment(
    int Number,
    DateTime DueDate,
    decimal OpeningBalance,
    decimal InterestAmount,
    decimal PrincipalAmount,
    decimal InstallmentAmount,
    decimal ClosingBalance);

/// <summary>
/// Calcul d'un échéancier d'emprunt (annuité constante ou amortissement constant). Fonction PURE :
/// aucune I/O, aucune dépendance base — donc exhaustivement testable.
/// <para>
/// <b>Invariant d'arrondi (millime).</b> Chaque montant est arrondi via <see cref="MillimeRounding"/>,
/// et la DERNIÈRE échéance absorbe l'écart cumulé : par construction,
/// <c>Σ PrincipalAmount == principal</c> exactement et le <c>ClosingBalance</c> final vaut 0.
/// Un échéancier qui ne se solde pas est un bug, pas une approximation.
/// </para>
/// </summary>
public static class LoanScheduleCalculator
{
    /// <summary>
    /// Construit l'échéancier. Les paramètres sont supposés déjà validés par <c>Loan.Create</c> ;
    /// des gardes défensives lèvent une exception si l'appelant les contourne.
    /// </summary>
    public static IReadOnlyList<LoanInstallment> Build(
        decimal principal,
        decimal annualRatePercent,
        int installmentCount,
        LoanPeriodicity periodicity,
        LoanAmortizationMethod method,
        DateTime startDate)
    {
        if (principal <= 0m)
            throw new ArgumentOutOfRangeException(nameof(principal), "Le capital emprunté doit être strictement positif.");
        if (annualRatePercent < 0m)
            throw new ArgumentOutOfRangeException(nameof(annualRatePercent), "Le taux ne peut pas être négatif.");
        if (installmentCount < 1)
            throw new ArgumentOutOfRangeException(nameof(installmentCount), "Il faut au moins une échéance.");

        var periodicRate = (annualRatePercent / 100m) / periodicity.PeriodsPerYear();
        var monthsStep = periodicity.MonthsPerPeriod();

        // Montant de référence par échéance, calculé une seule fois puis arrondi au millime.
        var reference = method == LoanAmortizationMethod.ConstantAnnuity
            ? MillimeRounding.Round(ConstantAnnuity(principal, periodicRate, installmentCount))
            : MillimeRounding.Round(principal / installmentCount);

        var schedule = new List<LoanInstallment>(installmentCount);
        var balance = principal;
        var firstDue = startDate.Date;

        for (var n = 1; n <= installmentCount; n++)
        {
            var opening = balance;
            var interest = MillimeRounding.Round(opening * periodicRate);

            decimal principalPart;
            if (n == installmentCount)
            {
                // Dernière échéance : elle solde le capital restant, absorbant tout écart d'arrondi.
                principalPart = opening;
            }
            else
            {
                principalPart = method == LoanAmortizationMethod.ConstantAnnuity
                    ? reference - interest
                    : reference;

                // Garde-fou : jamais plus que le capital restant, jamais négatif (taux très élevé).
                principalPart = Math.Clamp(MillimeRounding.Round(principalPart), 0m, opening);
            }

            var closing = MillimeRounding.Round(opening - principalPart);
            schedule.Add(new LoanInstallment(
                Number: n,
                DueDate: firstDue.AddMonths(monthsStep * n),
                OpeningBalance: opening,
                InterestAmount: interest,
                PrincipalAmount: principalPart,
                InstallmentAmount: MillimeRounding.Round(principalPart + interest),
                ClosingBalance: closing));

            balance = closing;
        }

        return schedule;
    }

    /// <summary>
    /// Annuité constante : <c>A = P · i / (1 − (1+i)^−n)</c>. Taux nul ⇒ <c>A = P / n</c>
    /// (la formule dégénère en 0/0).
    /// </summary>
    private static decimal ConstantAnnuity(decimal principal, decimal periodicRate, int count)
    {
        if (periodicRate == 0m)
            return principal / count;

        // (1+i)^-n via double : la précision décimale est rétablie par l'arrondi au millime,
        // et la dernière échéance absorbe de toute façon le résidu.
        var discount = 1d - Math.Pow(1d + (double)periodicRate, -count);
        return principal * periodicRate / (decimal)discount;
    }
}
