using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Paramètres du module Immobilisations pour un dossier (tenant) — support des exercices
/// comptables décalés (plan « Exercices décalés », décision D1 = Variante B : périmètre
/// immobilisations uniquement ; D3 = granularité par tenant).
/// </summary>
/// <remarks>
/// Singleton par tenant (une ligne au plus par base tenant). <see cref="FiscalYearStartMonth"/>
/// est le mois de début d'exercice (1 = janvier = exercice civil, comportement historique
/// intégralement préservé). <see cref="FiscalYearLabelFormat"/> pilote le libellé d'affichage
/// « N/N+1 » (ex. « 2026/2027 ») ou « N » (ex. « 2026 ») ; un exercice civil (mois 1) est
/// toujours libellé « N » quelle que soit la valeur du format (décision D2).
/// </remarks>
public sealed class FixedAssetSettings : Entity
{
    public const string LabelFormatNn1 = "N/N+1";
    public const string LabelFormatN = "N";
    public const int DefaultFiscalYearStartMonth = 1;

    public int FiscalYearStartMonth { get; private set; } = DefaultFiscalYearStartMonth;
    public string FiscalYearLabelFormat { get; private set; } = LabelFormatNn1;

    private FixedAssetSettings() { }

    public static Result<FixedAssetSettings> Create(int fiscalYearStartMonth, string fiscalYearLabelFormat)
    {
        var validation = Validate(fiscalYearStartMonth, fiscalYearLabelFormat);
        if (validation.IsFailure)
            return Result.Failure<FixedAssetSettings>(validation.Error);

        return Result.Success(new FixedAssetSettings
        {
            FiscalYearStartMonth = fiscalYearStartMonth,
            FiscalYearLabelFormat = NormalizeFormat(fiscalYearLabelFormat)
        });
    }

    /// <summary>Usine par défaut : exercice civil (mois 1), libellé « N/N+1 ».</summary>
    public static FixedAssetSettings CreateDefault() => new()
    {
        FiscalYearStartMonth = DefaultFiscalYearStartMonth,
        FiscalYearLabelFormat = LabelFormatNn1
    };

    public Result Update(int fiscalYearStartMonth, string fiscalYearLabelFormat)
    {
        var validation = Validate(fiscalYearStartMonth, fiscalYearLabelFormat);
        if (validation.IsFailure)
            return validation;

        FiscalYearStartMonth = fiscalYearStartMonth;
        FiscalYearLabelFormat = NormalizeFormat(fiscalYearLabelFormat);
        return Result.Success();
    }

    private static Result Validate(int fiscalYearStartMonth, string fiscalYearLabelFormat)
    {
        if (fiscalYearStartMonth is < 1 or > 12)
            return Result.Failure(Error.Validation(
                "FiscalYearStartMonth",
                "Le mois de début d'exercice doit être compris entre 1 et 12."));

        var format = NormalizeFormat(fiscalYearLabelFormat);
        if (format != LabelFormatNn1 && format != LabelFormatN)
            return Result.Failure(Error.Validation(
                "FiscalYearLabelFormat",
                "Le format de libellé d'exercice doit être 'N/N+1' ou 'N'."));

        return Result.Success();
    }

    private static string NormalizeFormat(string fiscalYearLabelFormat) =>
        string.IsNullOrWhiteSpace(fiscalYearLabelFormat)
            ? LabelFormatNn1
            : fiscalYearLabelFormat.Trim();
}
