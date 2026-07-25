using System.Globalization;
using FactuTrust.Domain.Entities.FirmGovernance;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Services.FirmGovernance;

/// <summary>Nature d'une anomalie détectée sur une saisie de temps.</summary>
public enum TimeSheetAnomalyKind
{
    /// <summary>Le cumul de la journée dépasse le plafond journalier du régime.</summary>
    DailyLimitExceeded = 1,
    /// <summary>Le cumul de la semaine ISO dépasse la durée hebdomadaire du régime.</summary>
    WeeklyLimitExceeded = 2,
    /// <summary>La date de travail est postérieure à la tolérance de saisie en avance.</summary>
    FutureDate = 3,
    /// <summary>La date de travail est antérieure à l'antériorité maximale autorisée.</summary>
    TooOld = 4
}

/// <summary>Une anomalie, avec de quoi la restituer telle quelle à l'utilisateur.</summary>
public sealed record TimeSheetAnomaly(TimeSheetAnomalyKind Kind, string Message);

/// <summary>
/// Contrôles de durée légale et de datation sur une saisie de temps.
/// </summary>
/// <remarks>
/// <para>
/// Service de domaine pur : aucune dépendance à la base ni à l'horloge système. L'appelant fournit
/// les cumuls déjà constatés et la date du jour, ce qui rend chaque règle testable isolément et
/// évite les tests dépendants du fuseau du serveur.
/// </para>
/// <para>
/// Le validateur ne décide pas du blocage : il retourne les anomalies constatées et l'appelant
/// consulte <see cref="FirmTimeSheetYearSettings.EnforceHardLimits"/> pour trancher entre refus et
/// simple avertissement. Les exercices antérieurs restent ainsi modifiables.
/// </para>
/// </remarks>
public static class TimeSheetLegalValidator
{
    /// <summary>
    /// Applique l'ensemble des règles à une saisie.
    /// </summary>
    /// <param name="workDate">Date de travail déclarée.</param>
    /// <param name="hours">Heures de la saisie en cours (création ou nouvelle valeur en modification).</param>
    /// <param name="otherHoursSameDay">
    /// Cumul des autres lignes du même collaborateur le même jour, ligne en cours d'édition exclue.
    /// </param>
    /// <param name="otherHoursSameWeek">
    /// Cumul des autres lignes du même collaborateur sur la même semaine ISO, ligne en cours exclue.
    /// </param>
    /// <param name="today">Date du jour dans le fuseau du cabinet (jamais <c>DateTime.UtcNow</c> brut).</param>
    /// <param name="settings">Paramètres de l'exercice concerné.</param>
    public static IReadOnlyList<TimeSheetAnomaly> Validate(
        DateTime workDate,
        decimal hours,
        decimal otherHoursSameDay,
        decimal otherHoursSameWeek,
        DateTime today,
        FirmTimeSheetYearSettings settings)
    {
        var anomalies = new List<TimeSheetAnomaly>();
        anomalies.AddRange(ValidateWorkDate(workDate, today, settings));

        var dailyTotal = otherHoursSameDay + hours;
        if (dailyTotal > settings.MaxDailyHours)
        {
            anomalies.Add(new TimeSheetAnomaly(
                TimeSheetAnomalyKind.DailyLimitExceeded,
                $"Le total du {workDate:dd/MM/yyyy} atteindrait {Fmt(dailyTotal)} h, "
                + $"au-delà du plafond journalier de {Fmt(settings.MaxDailyHours)} h."));
        }

        var weeklyTotal = otherHoursSameWeek + hours;
        if (weeklyTotal > settings.MaxWeeklyHours)
        {
            anomalies.Add(new TimeSheetAnomaly(
                TimeSheetAnomalyKind.WeeklyLimitExceeded,
                $"Le total de la semaine {IsoWeekOf(workDate)} atteindrait {Fmt(weeklyTotal)} h, "
                + $"au-delà de la durée hebdomadaire de {Fmt(settings.MaxWeeklyHours)} h "
                + $"({settings.WeeklyRegime.ToDisplayString()})."));
        }

        return anomalies;
    }

    /// <summary>Contrôle de datation seul : futur au-delà de la tolérance, antériorité excessive.</summary>
    public static IReadOnlyList<TimeSheetAnomaly> ValidateWorkDate(
        DateTime workDate,
        DateTime today,
        FirmTimeSheetYearSettings settings)
    {
        var anomalies = new List<TimeSheetAnomaly>();
        var date = workDate.Date;
        var reference = today.Date;

        var latestAllowed = reference.AddDays(settings.AllowFutureEntryDays);
        if (date > latestAllowed)
        {
            anomalies.Add(new TimeSheetAnomaly(
                TimeSheetAnomalyKind.FutureDate,
                settings.AllowFutureEntryDays == 0
                    ? $"La date {date:dd/MM/yyyy} est dans le futur : un temps non encore travaillé ne peut pas être déclaré."
                    : $"La date {date:dd/MM/yyyy} dépasse la tolérance de saisie en avance "
                      + $"({settings.AllowFutureEntryDays} j)."));
        }

        var earliestAllowed = reference.AddDays(-settings.MaxBackdatingDays);
        if (date < earliestAllowed)
        {
            anomalies.Add(new TimeSheetAnomaly(
                TimeSheetAnomalyKind.TooOld,
                $"La date {date:dd/MM/yyyy} dépasse l'antériorité maximale de {settings.MaxBackdatingDays} j "
                + $"(saisie possible à partir du {earliestAllowed:dd/MM/yyyy})."));
        }

        return anomalies;
    }

    /// <summary>
    /// Bornes de la semaine ISO contenant <paramref name="date"/> : lundi 00:00 → dimanche inclus.
    /// </summary>
    /// <remarks>
    /// La semaine ISO chevauche fréquemment deux mois, et parfois deux années (semaine 53) : le
    /// cumul hebdomadaire doit donc être calculé sur cet intervalle et non sur le mois affiché.
    /// </remarks>
    public static (DateTime Start, DateTime EndInclusive) IsoWeekBounds(DateTime date)
    {
        var day = date.Date;
        // DayOfWeek place dimanche à 0 ; la semaine ISO commence le lundi.
        var offset = ((int)day.DayOfWeek + 6) % 7;
        var monday = day.AddDays(-offset);
        return (monday, monday.AddDays(6));
    }

    /// <summary>Numéro de semaine ISO 8601, pour les libellés d'anomalie.</summary>
    public static int IsoWeekOf(DateTime date) =>
        ISOWeek.GetWeekOfYear(date.Date);

    private static string Fmt(decimal value) =>
        value.ToString("0.##", CultureInfo.GetCultureInfo("fr-FR"));
}
