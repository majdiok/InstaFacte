using FactuTrust.Application.DTOs;

namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Exports du module Immobilisations (plan « Exercices décalés », P4).
/// <para>
/// <paramref name="fiscalYearStartMonth"/> (1 = exercice civil, défaut usine) et
/// <paramref name="fiscalYearLabelFormat"/> (« N/N+1 » par défaut, décision D2) pilotent le libellé
/// d'affichage <c>N/N+1</c> et la résolution des dates de fin d'exercice dans les feuilles.
/// En exercice civil (<paramref name="fiscalYearStartMonth"/> = 1), les sorties sont bit-à-bit
/// identiques à l'existant (libellés « N », mêmes lignes/colonnes/montants) — les valeurs par défaut
/// reproduisent le comportement historique.
/// </para>
/// </summary>
public interface IFixedAssetExportService
{
    byte[] ExportScheduleToExcel(
        FixedAssetScheduleDto schedule,
        int fiscalYearStartMonth = 1,
        string fiscalYearLabelFormat = "N/N+1");

    byte[] ExportDepreciationReportToExcel(
        IReadOnlyList<FixedAssetScheduleDto> schedules,
        int fiscalYear,
        int fiscalYearStartMonth = 1,
        string fiscalYearLabelFormat = "N/N+1");

    byte[] ExportAmortizationReportToExcel(
        AmortizationReportResponse report,
        int fiscalYearStartMonth = 1,
        string fiscalYearLabelFormat = "N/N+1");
}
