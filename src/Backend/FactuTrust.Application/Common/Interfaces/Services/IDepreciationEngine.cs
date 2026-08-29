using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Common.Interfaces.Services;

public interface IDepreciationEngine
{
    /// <summary>
    /// Construit l'échéancier d'amortissement complet (prorata temporis sur les exercices de mise
    /// en service / de cession). Les lignes sont générées par <b>exercice</b> (clé = année de début
    /// d'exercice, <see cref="DepreciationScheduleLine.FiscalYear"/>). <paramref name="throughFiscalYear"/>
    /// (optionnel) est une clé d'exercice. <paramref name="fiscalYearStartMonth"/> = 1 = exercice civil
    /// (comportement historique bit-à-bit identique).
    /// </summary>
    IReadOnlyList<DepreciationScheduleLine> GenerateSchedule(
        FixedAsset asset,
        int? throughFiscalYear = null,
        int fiscalYearStartMonth = 1);

    /// <summary>
    /// Dotation prorata de l'exercice de cession (jours en service durant cet exercice).
    /// <paramref name="fiscalYear"/> est la clé d'exercice de la date de cession.
    /// <paramref name="fiscalYearStartMonth"/> = 1 = exercice civil.
    /// </summary>
    decimal CalculateDisposalYearDepreciation(
        FixedAsset asset,
        int fiscalYear,
        decimal priorAccumulatedDepreciation,
        int fiscalYearStartMonth = 1);
}