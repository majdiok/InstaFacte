using ClosedXML.Excel;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.FixedAssets;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Services;
using Xunit;

namespace FactuTrust.Infrastructure.Tests.Services;

/// <summary>
/// Golden tests des exports Immobilisations (plan « Exercices décalés », P4) :
/// (1) parité stricte janvier — <c>fiscalYearStartMonth=1</c> produit des cellules d'exercice
/// <b>numériques</b> et les en-têtes <c>31/12/{year}</c> / « Exercice du … au … » bit-à-bit identiques
/// à l'existant, format de libellé ignoré ; (2) cas décalés — libellés <c>N/N+1</c>, dates de fin
/// d'exercice réelles, et résolution correcte de la ligne d'exercice par clé.
/// </summary>
public sealed class FixedAssetExportServiceOffsetFiscalYearTests
{
    private readonly FixedAssetExportService _export = new();

    private static readonly Guid AssetId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccc0001");

    private static FixedAssetScheduleDto BuildSchedule(
        params (int FiscalYear, decimal DepreciationAmount, decimal Accumulated, decimal ClosingNbv, decimal PriorAcc)[] lines)
    {
        var dtos = lines.Select((l, i) => new DepreciationScheduleLineDto(
            Guid.NewGuid(),
            l.FiscalYear,
            PeriodMonth: 12,
            OpeningNbv: 40_000m - l.Accumulated,
            NormalAnnualAmount: 8_000m,
            l.PriorAcc,
            l.DepreciationAmount,
            l.Accumulated,
            l.ClosingNbv,
            IsPosted: true,
            IsReversed: false)).ToList();

        return new FixedAssetScheduleDto(
            AssetId,
            "IMMO-2026-0001",
            "Voiture",
            new DateTime(2026, 1, 10),
            InServiceDate: new DateTime(2026, 1, 10),
            TotalCapitalizedCost: 40_000m,
            DepreciationRatePercent: 20m,
            UsefulLifeYears: 5m,
            DepreciableBase: 40_000m,
            dtos,
            DepreciationMethod.Linear,
            AccelerationCoefficient: 1m);
    }

    private static XLWorkbook OpenWorkbook(byte[] bytes)
    {
        var ms = new MemoryStream(bytes);
        return new XLWorkbook(ms);
    }

    // ------------------------------------------------------------------
    // Tableau d'amortissement officiel — colonne « Année » (libellé d'exercice)
    // ------------------------------------------------------------------

    [Fact]
    public void ScheduleExcel_CivilStart_YearCellsShouldBeNumericIntLabels()
    {
        // Parité janvier : la colonne Année porte l'entier brut (cellule numérique), bit-à-bit identique
        // à l'existant. Le format de libellé est ignoré en exercice civil.
        var schedule = BuildSchedule(
            (2026, 8_000m, 8_000m, 32_000m, 0m),
            (2027, 8_000m, 16_000m, 24_000m, 8_000m));

        using var wb = OpenWorkbook(_export.ExportScheduleToExcel(schedule, fiscalYearStartMonth: 1, "N/N+1"));
        var ws = wb.Worksheet("Tableau amortissement");

        // headerRow = 10 (6 lignes d'info depuis r=3 → r=9, headerRow=r+1=10), lignes à partir de 11.
        Assert.Equal("Année", ws.Cell(10, 1).GetString());
        Assert.Equal(XLDataType.Number, ws.Cell(11, 1).DataType);
        Assert.Equal("2026", ws.Cell(11, 1).GetString());
        Assert.Equal(XLDataType.Number, ws.Cell(12, 1).DataType);
        Assert.Equal("2027", ws.Cell(12, 1).GetString());
    }

    [Fact]
    public void ScheduleExcel_CivilStart_LabelFormatIgnored_IdenticalLabels()
    {
        // En exercice civil, le format (« N/N+1 » vs « N ») ne change rien : toujours « N » (2026).
        var schedule = BuildSchedule((2026, 8_000m, 8_000m, 32_000m, 0m));

        using var wbA = OpenWorkbook(_export.ExportScheduleToExcel(schedule, 1, "N/N+1"));
        using var wbB = OpenWorkbook(_export.ExportScheduleToExcel(schedule, 1, "N"));
        var wsA = wbA.Worksheet("Tableau amortissement");
        var wsB = wbB.Worksheet("Tableau amortissement");

        Assert.Equal(wsA.Cell(11, 1).DataType, wsB.Cell(11, 1).DataType);
        Assert.Equal(wsA.Cell(11, 1).GetString(), wsB.Cell(11, 1).GetString());
        Assert.Equal("2026", wsB.Cell(11, 1).GetString());
    }

    [Fact]
    public void ScheduleExcel_OffsetStart_YearCellsShouldBeNn1Labels()
    {
        // Exercice décalé juil.→juin : clé 2026 → « 2026/2027 », clé 2027 → « 2027/2028 » (cellules texte).
        var schedule = BuildSchedule(
            (2026, 8_000m, 8_000m, 32_000m, 0m),
            (2027, 8_000m, 16_000m, 24_000m, 8_000m));

        using var wb = OpenWorkbook(_export.ExportScheduleToExcel(schedule, fiscalYearStartMonth: 7, "N/N+1"));
        var ws = wb.Worksheet("Tableau amortissement");

        Assert.Equal("Année", ws.Cell(10, 1).GetString());
        Assert.Equal(XLDataType.Text, ws.Cell(11, 1).DataType);
        Assert.Equal("2026/2027", ws.Cell(11, 1).GetString());
        Assert.Equal(XLDataType.Text, ws.Cell(12, 1).DataType);
        Assert.Equal("2027/2028", ws.Cell(12, 1).GetString());
    }

    [Fact]
    public void ScheduleExcel_OffsetStart_NFormat_ShouldUseStartYearOnly()
    {
        var schedule = BuildSchedule((2026, 8_000m, 8_000m, 32_000m, 0m));

        using var wb = OpenWorkbook(_export.ExportScheduleToExcel(schedule, fiscalYearStartMonth: 7, "N"));
        var ws = wb.Worksheet("Tableau amortissement");

        Assert.Equal("2026", ws.Cell(11, 1).GetString());
    }

    // ------------------------------------------------------------------
    // Détail CP17 — colonne « Exercice »
    // ------------------------------------------------------------------

    [Fact]
    public void Cp17Detail_CivilStart_ExerciseColumnShouldBeNumericInt()
    {
        var schedule = BuildSchedule((2026, 8_000m, 8_000m, 32_000m, 0m));

        using var wb = OpenWorkbook(_export.ExportScheduleToExcel(schedule, 1, "N/N+1"));
        var ws = wb.Worksheet("Détail CP17");

        // headerRow = 10 (fixe), lignes à partir de 11 ; colonne 1 = « Exercice ».
        Assert.Equal("Exercice", ws.Cell(10, 1).GetString());
        Assert.Equal(XLDataType.Number, ws.Cell(11, 1).DataType);
        Assert.Equal("2026", ws.Cell(11, 1).GetString());
    }

    [Fact]
    public void Cp17Detail_OffsetStart_ExerciseColumnShouldBeNn1Label()
    {
        var schedule = BuildSchedule((2026, 8_000m, 8_000m, 32_000m, 0m));

        using var wb = OpenWorkbook(_export.ExportScheduleToExcel(schedule, 7, "N/N+1"));
        var ws = wb.Worksheet("Détail CP17");

        Assert.Equal(XLDataType.Text, ws.Cell(11, 1).DataType);
        Assert.Equal("2026/2027", ws.Cell(11, 1).GetString());
    }

    // ------------------------------------------------------------------
    // Export dotations — nom de feuille + résolution de la ligne d'exercice par clé
    // ------------------------------------------------------------------

    [Fact]
    public void DepreciationReport_CivilStart_SheetNameShouldBeCalendarYear()
    {
        var schedule = BuildSchedule((2026, 8_000m, 8_000m, 32_000m, 0m));
        var bytes = _export.ExportDepreciationReportToExcel(new[] { schedule }, fiscalYear: 2026, fiscalYearStartMonth: 1, "N/N+1");

        using var wb = OpenWorkbook(bytes);
        Assert.Equal("Dotations 2026", wb.Worksheets.First().Name);
    }

    [Fact]
    public void DepreciationReport_OffsetStart_SheetNameShouldBeNn1Label()
    {
        // Le nom de feuille Excel n'accepte pas le « / » : le libellé N/N+1 y est rendu « 2026-2027 ».
        var schedule = BuildSchedule((2026, 8_000m, 8_000m, 32_000m, 0m));
        var bytes = _export.ExportDepreciationReportToExcel(new[] { schedule }, fiscalYear: 2026, fiscalYearStartMonth: 7, "N/N+1");

        using var wb = OpenWorkbook(bytes);
        Assert.Equal("Dotations 2026-2027", wb.Worksheets.First().Name);
    }

    [Fact]
    public void DepreciationReport_ShouldResolveCorrectFiscalYearLineByKey()
    {
        // Deux biens, chacun avec les lignes clés 2026 et 2027. L'export pour l'exercice 2027 doit
        // sélectionner la ligne de clé 2027 (montant 7_000) — la résolution par clé est indépendante
        // du mois de début d'exercice (P2 : lignes étiquetées par clé).
        var schedA = BuildSchedule(
            (2026, 8_000m, 8_000m, 32_000m, 0m),
            (2027, 7_000m, 15_000m, 25_000m, 8_000m));
        var schedB = BuildSchedule(
            (2026, 4_000m, 4_000m, 16_000m, 0m),
            (2027, 7_000m, 11_000m, 9_000m, 4_000m));

        var bytes = _export.ExportDepreciationReportToExcel(new[] { schedA, schedB }, fiscalYear: 2027, fiscalYearStartMonth: 7, "N/N+1");
        using var wb = OpenWorkbook(bytes);
        var ws = wb.Worksheets.First();

        // En-têtes en ligne 1 ; données à partir de la ligne 2. Colonne 11 = « Dotation exercice ».
        Assert.Equal("Dotation exercice", ws.Cell(1, 11).GetString());
        Assert.Equal(7_000m, ws.Cell(2, 11).GetValue<decimal>());   // schedA, ligne clé 2027
        Assert.Equal(7_000m, ws.Cell(3, 11).GetValue<decimal>());   // schedB, ligne clé 2027
    }

    // ------------------------------------------------------------------
    // Rapport d'amortissement — en-tête d'exercice (libellé N/N+1 + dates de fin d'exercice)
    // ------------------------------------------------------------------

    private static AmortizationReportResponse BuildReport(int fiscalYear, int startMonth)
    {
        var projection = new AmortizationReportAssetProjection(
            AssetId,
            "224",
            "IMMO-2026-0001",
            "Voiture",
            new DateTime(2026, 1, 10),
            40_000m,
            5m,
            DepreciationMethod.Linear,
            "VEH_PASS",
            "Véhicules de tourisme",
            PriorAccumulatedDepreciation: 8_000m,
            DotationCalculeeExercice: 8_000m,
            DotationComptabiliseeExercice: 8_000m,
            EndOfYearAccumulatedDepreciation: 16_000m,
            EndOfYearNetBookValue: 24_000m,
            TotalLineCount: 1,
            PostedLineCount: 1);
        return AmortizationReportAssembler.Assemble(
            new[] { projection }, fiscalYear, AmortizationReportGroupingMode.AssetAccount, "Société Test", startMonth);
    }

    [Fact]
    public void AmortizationReport_CivilStart_HeadersShouldBeDec31BitIdentical()
    {
        // Parité janvier : en-têtes « 31/12/{priorYear} » / « 31/12/{fy} » et ligne d'exercice
        // « Exercice du 01/01/N au 31/12/N » — strictement identiques à l'existant.
        var report = BuildReport(fiscalYear: 2026, startMonth: 1);
        using var wb = OpenWorkbook(_export.ExportAmortizationReportToExcel(report, 1, "N/N+1"));
        var ws = wb.Worksheet("Tableau amortissements");

        Assert.Equal("Exercice du 01/01/2026 au 31/12/2026", ws.Cell(3, 1).GetString());
        Assert.Equal("Amort. antérieurs 31/12/2025", ws.Cell(5, 7).GetString());
        Assert.Equal("Fin exercice 31/12/2026", ws.Cell(5, 10).GetString());
        Assert.Equal("VNC 31/12/2026", ws.Cell(5, 11).GetString());
    }

    [Fact]
    public void AmortizationReport_CivilStart_LabelFormatIgnored_IdenticalHeaders()
    {
        var report = BuildReport(2026, 1);
        using var wbA = OpenWorkbook(_export.ExportAmortizationReportToExcel(report, 1, "N/N+1"));
        using var wbB = OpenWorkbook(_export.ExportAmortizationReportToExcel(report, 1, "N"));
        var wsA = wbA.Worksheet("Tableau amortissements");
        var wsB = wbB.Worksheet("Tableau amortissements");

        Assert.Equal(wsA.Cell(3, 1).GetString(), wsB.Cell(3, 1).GetString());
        Assert.Equal(wsA.Cell(5, 10).GetString(), wsB.Cell(5, 10).GetString());
    }

    [Fact]
    public void AmortizationReport_OffsetStart_HeadersShouldUseFiscalYearEndAndNn1Label()
    {
        // Exercice 2026 (juil. 2026 → juin 2027) : fin exercice = 30/06/2027 ; fin exercice antérieur
        // (clé 2025) = 30/06/2026 ; libellé N/N+1 affiché en tête.
        var report = BuildReport(fiscalYear: 2026, startMonth: 7);
        using var wb = OpenWorkbook(_export.ExportAmortizationReportToExcel(report, 7, "N/N+1"));
        var ws = wb.Worksheet("Tableau amortissements");

        Assert.Equal("Exercice 2026/2027 — du 01/07/2026 au 30/06/2027", ws.Cell(3, 1).GetString());
        Assert.Equal("Amort. antérieurs 30/06/2026", ws.Cell(5, 7).GetString());
        Assert.Equal("Fin exercice 30/06/2027", ws.Cell(5, 10).GetString());
        Assert.Equal("VNC 30/06/2027", ws.Cell(5, 11).GetString());
    }

    [Fact]
    public void AmortizationReport_OffsetStart_NFormat_HeaderUsesStartYearOnly()
    {
        var report = BuildReport(2026, 7);
        using var wb = OpenWorkbook(_export.ExportAmortizationReportToExcel(report, 7, "N"));
        var ws = wb.Worksheet("Tableau amortissements");

        // Format « N » : libellé = « 2026 » (mais les dates de fin d'exercice restent décalées).
        Assert.Equal("Exercice 2026 — du 01/07/2026 au 30/06/2027", ws.Cell(3, 1).GetString());
        Assert.Equal("Fin exercice 30/06/2027", ws.Cell(5, 10).GetString());
    }

    [Fact]
    public void AmortizationReport_SummarySheet_OffsetStart_HeadersShouldUseFiscalYearEnd()
    {
        var report = BuildReport(2026, 7);
        using var wb = OpenWorkbook(_export.ExportAmortizationReportToExcel(report, 7, "N/N+1"));
        var ws = wb.Worksheet("Récapitulatif");

        // headerRow = 3 ; colonne 3 = amort. cumulés fin exercice antérieur, 4 = dotation exercice, 5 = VNC.
        Assert.Equal("Amort. cumulés 30/06/2026", ws.Cell(3, 3).GetString());
        Assert.Equal("Dotation exercice 2026/2027", ws.Cell(3, 4).GetString());
        Assert.Equal("VNC 30/06/2027", ws.Cell(3, 5).GetString());
    }

    [Fact]
    public void AmortizationReport_SummarySheet_CivilStart_HeadersShouldBeDec31BitIdentical()
    {
        var report = BuildReport(2026, 1);
        using var wb = OpenWorkbook(_export.ExportAmortizationReportToExcel(report, 1, "N/N+1"));
        var ws = wb.Worksheet("Récapitulatif");

        Assert.Equal("Amort. cumulés 31/12/2025", ws.Cell(3, 3).GetString());
        Assert.Equal("Dotation exercice 2026", ws.Cell(3, 4).GetString());
        Assert.Equal("VNC 31/12/2026", ws.Cell(3, 5).GetString());
    }
}
