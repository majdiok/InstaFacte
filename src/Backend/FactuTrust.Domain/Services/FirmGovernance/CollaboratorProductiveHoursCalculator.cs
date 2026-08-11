using System.Globalization;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.FirmGovernance;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Services.FirmGovernance;

/// <summary>
/// Heures productives retenues pour un collaborateur, et la façon dont elles ont été obtenues.
/// </summary>
/// <param name="Hours">Dénominateur du taux horaire de revient.</param>
/// <param name="Mode">Forfait commun ou individualisé.</param>
/// <param name="Basis">
/// Formulation lisible du calcul. C'est elle qui rend le taux auditable : un dénominateur
/// individualisé varie d'un collaborateur à l'autre, il doit pouvoir s'expliquer.
/// </param>
public sealed record FirmProductiveHoursResolution(
    decimal Hours,
    FirmProductiveHoursMode Mode,
    string Basis);

/// <summary>
/// Détermine les heures productives annuelles d'un collaborateur pour un exercice.
/// </summary>
/// <remarks>
/// <para>
/// En mode paramétrique, délègue strictement à <see cref="FirmTimeSheetYearSettings.AnnualProductiveHours"/> :
/// aucun exercice existant ne doit changer de valeur.
/// </para>
/// <para>
/// En mode individualisé, les congés réellement approuvés remplacent le forfait de congés payés,
/// et la présence effective du collaborateur (entrée ou sortie en cours d'exercice) réduit la base
/// théorique. Les jours fériés restent paramétriques : ils sont les mêmes pour tout le monde.
/// </para>
/// </remarks>
public static class CollaboratorProductiveHoursCalculator
{
    public static FirmProductiveHoursResolution Resolve(
        FirmTimeSheetYearSettings settings,
        decimal realAbsenceDays,
        decimal presenceRatio)
    {
        if (settings.ProductiveHoursMode != FirmProductiveHoursMode.IndividualRealLeaves)
        {
            return new FirmProductiveHoursResolution(
                settings.AnnualProductiveHours,
                FirmProductiveHoursMode.Parametric,
                $"{Fmt(settings.AnnualProductiveHours)} h productives — forfait de l'exercice");
        }

        var ratio = Clamp(presenceRatio);
        var absences = realAbsenceDays < 0m ? 0m : realAbsenceDays;

        var baseHours = MillimeRounding.Round(settings.AnnualBaseHours * ratio);
        var holidayDays = MillimeRounding.Round(settings.PublicHolidayDaysPerYear * ratio);
        var absenceHours = MillimeRounding.Round((absences + holidayDays) * settings.DailyHours);
        var presentHours = baseHours - absenceHours;

        if (presentHours <= 0m)
        {
            return new FirmProductiveHoursResolution(
                0m,
                FirmProductiveHoursMode.IndividualRealLeaves,
                "Aucune heure productive : les absences couvrent la totalité de la présence.");
        }

        var hours = MillimeRounding.Round(presentHours * settings.ProductivityRatePercent / 100m);
        var basis = ratio < 1m
            ? $"{Fmt(settings.AnnualBaseHours)} h × {Fmt(ratio)} présence − ({Fmt(absences)} j congés réels + {Fmt(holidayDays)} j fériés) × {Fmt(settings.DailyHours)} h, × {Fmt(settings.ProductivityRatePercent)} %"
            : $"{Fmt(settings.AnnualBaseHours)} h − ({Fmt(absences)} j congés réels + {Fmt(holidayDays)} j fériés) × {Fmt(settings.DailyHours)} h, × {Fmt(settings.ProductivityRatePercent)} %";

        return new FirmProductiveHoursResolution(hours, FirmProductiveHoursMode.IndividualRealLeaves, basis);
    }

    /// <summary>
    /// Part de l'exercice réellement couverte par la présence du collaborateur.
    /// </summary>
    /// <remarks>
    /// Deux dates nulles donnent 1 : c'est le cas de tous les collaborateurs en place, et cela
    /// garantit que l'ajout de ces dates ne change rien tant qu'elles ne sont pas renseignées.
    /// </remarks>
    public static decimal ComputePresenceRatio(int year, DateTime? hiredOn, DateTime? leftOn)
    {
        if (hiredOn is null && leftOn is null)
            return 1m;

        var yearStart = new DateTime(year, 1, 1);
        var yearEnd = new DateTime(year, 12, 31);

        var start = hiredOn is { } h && h.Date > yearStart ? h.Date : yearStart;
        var end = leftOn is { } l && l.Date < yearEnd ? l.Date : yearEnd;
        if (end < start)
            return 0m;

        var totalDays = (yearEnd - yearStart).Days + 1;
        var presentDays = (end - start).Days + 1;
        return MillimeRounding.Round((decimal)presentDays / totalDays);
    }

    private static decimal Clamp(decimal ratio) => ratio switch
    {
        <= 0m => 0m,
        >= 1m => 1m,
        _ => ratio
    };

    private static string Fmt(decimal value) =>
        value.ToString("#,##0.##", CultureInfo.GetCultureInfo("fr-FR"));
}
