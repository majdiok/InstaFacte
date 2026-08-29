using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Entities;

namespace FactuTrust.Infrastructure.Services;

/// <summary>
/// Implémentation déterministe de <see cref="IFiscalYearResolver"/> (plan « Exercices décalés »).
/// Pure : aucune dépendance à la configuration ou à la base. Les calculs de jours 30/360
/// relatifs à la frontière d'exercice vivent dans <see cref="DepreciationEngine"/> (moteur) ;
/// ce résolveur ne traite que la clé, les bornes calendaires et le libellé.
/// </summary>
public sealed class FiscalYearResolver : IFiscalYearResolver
{
    public int FiscalYearKey(DateTime date, int fiscalYearStartMonth)
    {
        ValidateStartMonth(fiscalYearStartMonth);
        return date.Month >= fiscalYearStartMonth ? date.Year : date.Year - 1;
    }

    public DateTime FiscalYearStartDateTime(int fiscalYearKey, int fiscalYearStartMonth)
    {
        ValidateStartMonth(fiscalYearStartMonth);
        return new DateTime(fiscalYearKey, fiscalYearStartMonth, 1);
    }

    public DateTime FiscalYearEndDateTime(int fiscalYearKey, int fiscalYearStartMonth)
    {
        ValidateStartMonth(fiscalYearStartMonth);
        // L'exercice suivant débute le 1er du mois de début, l'année (key+1) — la fin d'exercice
        // est la veille. AddDays(-1) gère nativement les années bissextiles (28/29 février).
        return new DateTime(fiscalYearKey + 1, fiscalYearStartMonth, 1).AddDays(-1);
    }

    public string FiscalYearLabel(int fiscalYearKey, int fiscalYearStartMonth, string labelFormat)
    {
        ValidateStartMonth(fiscalYearStartMonth);

        // Exercice civil (janvier) → toujours « N » (décision D2).
        if (fiscalYearStartMonth == 1)
            return fiscalYearKey.ToString();

        // Exercice décalé : « N » → année de début ; tout le reste → « N/N+1 ».
        if (string.Equals(labelFormat, FixedAssetSettings.LabelFormatN, StringComparison.Ordinal))
            return fiscalYearKey.ToString();

        return $"{fiscalYearKey}/{fiscalYearKey + 1}";
    }

    private static void ValidateStartMonth(int fiscalYearStartMonth)
    {
        if (fiscalYearStartMonth is < 1 or > 12)
            throw new ArgumentOutOfRangeException(
                nameof(fiscalYearStartMonth),
                "Le mois de début d'exercice doit être compris entre 1 et 12.");
    }
}
