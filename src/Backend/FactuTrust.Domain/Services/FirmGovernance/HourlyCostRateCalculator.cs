using System.Globalization;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.FirmGovernance;

namespace FactuTrust.Domain.Services.FirmGovernance;

/// <summary>Provenance du taux horaire de revient retenu pour un collaborateur.</summary>
public enum FirmHourlyRateSource
{
    /// <summary>Calculé : coût employeur annuel ÷ heures productives annuelles.</summary>
    Derived = 1,
    /// <summary>Imposé par le cabinet, avec justification.</summary>
    Override = 2,
    /// <summary>Repli historique : taux porté par le profil du collaborateur.</summary>
    LegacyProfile = 3,
    /// <summary>Repli final : taux par défaut du cabinet.</summary>
    FirmDefault = 4
}

/// <summary>
/// Taux horaire retenu, sa provenance et la façon dont il a été obtenu.
/// </summary>
/// <param name="Rate">Taux horaire de revient, en TND.</param>
/// <param name="Source">Origine du taux.</param>
/// <param name="Basis">
/// Formulation lisible du calcul, destinée à être affichée en regard du montant. C'est elle qui
/// rend la colonne « Taux horaire » auditable : sans elle, le chiffre reste invérifiable.
/// </param>
public sealed record FirmHourlyRateResolution(decimal Rate, FirmHourlyRateSource Source, string Basis);

/// <summary>
/// Détermine le taux horaire de revient d'un collaborateur pour un exercice.
/// </summary>
/// <remarks>
/// <para>
/// Chaîne de repli : taux imposé, puis taux dérivé du coût employeur, puis taux historique du
/// profil, puis taux par défaut du cabinet. Le premier niveau qui aboutit l'emporte.
/// </para>
/// <para>
/// Le taux imposé passe avant le taux dérivé : c'est un acte explicite du cabinet, assorti d'une
/// justification. Le placer après le calcul le rendrait inopérant dès qu'une paie existe — soit
/// exactement le défaut que ce module corrige, où le taux stocké n'était jamais lu.
/// </para>
/// </remarks>
public static class HourlyCostRateCalculator
{
    /// <summary>
    /// Taux horaire à partir des heures productives forfaitaires de l'exercice.
    /// </summary>
    public static FirmHourlyRateResolution Resolve(
        FirmCollaboratorYearCost? cost,
        FirmTimeSheetYearSettings settings,
        decimal? legacyProfileRate,
        decimal firmDefaultRate) =>
        Resolve(cost, settings.AnnualProductiveHours, legacyProfileRate, firmDefaultRate);

    /// <summary>
    /// Taux horaire à partir d'un dénominateur déjà résolu.
    /// </summary>
    /// <remarks>
    /// Surcharge introduite pour l'individualisation des heures productives : le dénominateur peut
    /// alors dépendre du collaborateur, et n'est plus déductible des seuls paramètres d'exercice.
    /// </remarks>
    public static FirmHourlyRateResolution Resolve(
        FirmCollaboratorYearCost? cost,
        decimal productiveHours,
        decimal? legacyProfileRate,
        decimal firmDefaultRate)
    {
        if (cost?.HourlyRateOverride is > 0)
        {
            var justification = string.IsNullOrWhiteSpace(cost.OverrideJustification)
                ? "taux imposé par le cabinet"
                : cost.OverrideJustification;
            return new FirmHourlyRateResolution(
                cost.HourlyRateOverride.Value,
                FirmHourlyRateSource.Override,
                $"Taux imposé — {justification}");
        }

        if (cost is not null && cost.TotalEmployerCost > 0 && productiveHours > 0)
        {
            var rate = MillimeRounding.Round(cost.TotalEmployerCost / productiveHours);
            return new FirmHourlyRateResolution(
                rate,
                FirmHourlyRateSource.Derived,
                $"{Fmt(cost.TotalEmployerCost)} TND ÷ {Fmt(productiveHours)} h productives");
        }

        if (legacyProfileRate is > 0)
        {
            return new FirmHourlyRateResolution(
                MillimeRounding.Round(legacyProfileRate.Value),
                FirmHourlyRateSource.LegacyProfile,
                "Taux porté par le profil du collaborateur");
        }

        var fallback = firmDefaultRate > 0 ? firmDefaultRate : 0m;
        return new FirmHourlyRateResolution(
            MillimeRounding.Round(fallback),
            FirmHourlyRateSource.FirmDefault,
            "Taux par défaut du cabinet — aucun coût employeur renseigné pour cet exercice");
    }

    private static string Fmt(decimal value) =>
        value.ToString("#,##0.###", CultureInfo.GetCultureInfo("fr-FR"));
}
