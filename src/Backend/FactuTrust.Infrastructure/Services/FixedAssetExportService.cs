using ClosedXML.Excel;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;

namespace FactuTrust.Infrastructure.Services;

public sealed class FixedAssetExportService : IFixedAssetExportService
{
    private readonly IFiscalYearResolver _fiscalYearResolver;

    /// <summary>
    /// Constructeur par défaut (résolveur déterministe canonique) — utilisé en test direct
    /// (<c>new FixedAssetExportService()</c>) et conserve le comportement historique.
    /// </summary>
    public FixedAssetExportService() : this(new FiscalYearResolver()) { }

    /// <summary>
    /// Constructeur DI : le résolveur est substituable en test (plan « Exercices décalés »).
    /// </summary>
    public FixedAssetExportService(IFiscalYearResolver fiscalYearResolver)
    {
        _fiscalYearResolver = fiscalYearResolver;
    }

    /// <summary>
    /// Valeur de cellule pour une colonne d'exercice : en exercice civil (<paramref name="fiscalYearStartMonth"/> = 1)
    /// retourne la clé entière brute (sortie bit-à-bit identique à l'existant) ; en exercice décalé retourne le
    /// libellé <c>N/N+1</c> (décision D2).
    /// </summary>
    private XLCellValue FiscalYearCell(int fiscalYearKey, int fiscalYearStartMonth, string fiscalYearLabelFormat)
    {
        if (fiscalYearStartMonth == 1)
            return fiscalYearKey;
        return _fiscalYearResolver.FiscalYearLabel(fiscalYearKey, fiscalYearStartMonth, fiscalYearLabelFormat);
    }

    public byte[] ExportScheduleToExcel(
        FixedAssetScheduleDto schedule,
        int fiscalYearStartMonth = 1,
        string fiscalYearLabelFormat = "N/N+1")
    {
        using var wb = new XLWorkbook();
        AddOfficialScheduleSheet(wb, schedule, fiscalYearStartMonth, fiscalYearLabelFormat);
        AddCp17DetailSheet(wb, schedule, fiscalYearStartMonth, fiscalYearLabelFormat);
        return WorkbookToBytes(wb);
    }

    /// <summary>
    /// Feuille au format du tableau d'amortissement officiel tunisien :
    /// en-tête récapitulatif (nature, montant, date, durée, taux) puis colonnes
    /// Année / Base / Annuité / Annuités cumulées / Valeur nette comptable + ligne TOTAL.
    /// </summary>
    private void AddOfficialScheduleSheet(
        XLWorkbook wb,
        FixedAssetScheduleDto schedule,
        int fiscalYearStartMonth,
        string fiscalYearLabelFormat)
    {
        var ws = wb.Worksheets.Add("Tableau amortissement");

        ws.Cell(1, 1).Value = "TABLEAU D'AMORTISSEMENT D'UNE IMMOBILISATION";
        ws.Range(1, 1, 1, 5).Merge();
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 13;
        ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        var infoRows = new (string Label, XLCellValue Value, string? Format)[]
        {
            ("Nature du bien", schedule.Label, null),
            ("Montant de l'achat", schedule.TotalCapitalizedCost, "#,##0.000"),
            ("Date de mise en service", schedule.InServiceDate.HasValue ? (XLCellValue)schedule.InServiceDate.Value : "—", "dd/MM/yyyy"),
            ("Durée de vie", $"{schedule.UsefulLifeYears:0.##} ans", null),
            ("Taux d'amortissement", schedule.DepreciationRatePercent / 100m, "0.00%"),
            ("Base amortissable", schedule.DepreciableBase, "#,##0.000")
        };

        var r = 3;
        foreach (var (label, value, format) in infoRows)
        {
            ws.Cell(r, 1).Value = label;
            ws.Cell(r, 1).Style.Font.Bold = true;
            ws.Cell(r, 2).Value = value;
            if (format is not null)
                ws.Cell(r, 2).Style.NumberFormat.Format = format;
            ws.Range(r, 1, r, 2).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            r++;
        }

        var headerRow = r + 1;
        var headers = new[] { "Année", "Base", "Annuité", "Annuités cumulées", "Valeur nette comptable" };
        for (var c = 0; c < headers.Length; c++)
            ws.Cell(headerRow, c + 1).Value = headers[c];
        StyleHeader(ws, headerRow, headers.Length);

        var row = headerRow + 1;
        foreach (var line in schedule.Lines)
        {
            ws.Cell(row, 1).Value = FiscalYearCell(line.FiscalYear, fiscalYearStartMonth, fiscalYearLabelFormat);
            ws.Cell(row, 2).Value = schedule.DepreciableBase;
            ws.Cell(row, 3).Value = line.DepreciationAmount;
            ws.Cell(row, 4).Value = line.AccumulatedDepreciation;
            ws.Cell(row, 5).Value = line.ClosingNbv;
            ApplyAmountFormat(ws, row, 2, 5);
            row++;
        }

        ws.Cell(row, 1).Value = "TOTAL";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 3).Value = schedule.Lines.Sum(l => l.DepreciationAmount);
        ws.Cell(row, 3).Style.Font.Bold = true;
        ApplyAmountFormat(ws, row, 3, 3);

        ws.Range(headerRow, 1, row, headers.Length).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        ws.Range(headerRow, 1, row, headers.Length).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        ws.Columns().AdjustToContents();
    }

    private void AddCp17DetailSheet(
        XLWorkbook wb,
        FixedAssetScheduleDto schedule,
        int fiscalYearStartMonth,
        string fiscalYearLabelFormat)
    {
        var ws = wb.Worksheets.Add("Détail CP17");

        ws.Cell(1, 1).Value = "N° inventaire";
        ws.Cell(1, 2).Value = schedule.InventoryNumber;
        ws.Cell(2, 1).Value = "Désignation";
        ws.Cell(2, 2).Value = schedule.Label;
        ws.Cell(3, 1).Value = "Date acquisition";
        ws.Cell(3, 2).Value = schedule.AcquisitionDate;
        ws.Cell(3, 2).Style.DateFormat.Format = "dd/MM/yyyy";
        ws.Cell(4, 1).Value = "Date mise en service";
        if (schedule.InServiceDate.HasValue)
        {
            ws.Cell(4, 2).Value = schedule.InServiceDate.Value;
            ws.Cell(4, 2).Style.DateFormat.Format = "dd/MM/yyyy";
        }
        ws.Cell(5, 1).Value = "Valeur origine";
        ws.Cell(5, 2).Value = schedule.TotalCapitalizedCost;
        ws.Cell(6, 1).Value = "Taux %";
        ws.Cell(6, 2).Value = schedule.DepreciationRatePercent;
        ws.Cell(7, 1).Value = "Durée (ans)";
        ws.Cell(7, 2).Value = schedule.UsefulLifeYears;
        ws.Cell(8, 1).Value = "Base amortissable";
        ws.Cell(8, 2).Value = schedule.DepreciableBase;

        var headers = new[]
        {
            "Exercice", "VNC début", "Annuité normale", "Amort. antérieurs",
            "Dotation exercice", "Cumul amort.", "VNC fin", "Comptabilisé"
        };
        var headerRow = 10;
        for (var c = 0; c < headers.Length; c++)
            ws.Cell(headerRow, c + 1).Value = headers[c];
        StyleHeader(ws, headerRow, headers.Length);

        var row = headerRow + 1;
        foreach (var line in schedule.Lines)
        {
            ws.Cell(row, 1).Value = FiscalYearCell(line.FiscalYear, fiscalYearStartMonth, fiscalYearLabelFormat);
            ws.Cell(row, 2).Value = line.OpeningNbv;
            ws.Cell(row, 3).Value = line.NormalAnnualAmount;
            ws.Cell(row, 4).Value = line.PriorAccumulatedDepreciation;
            ws.Cell(row, 5).Value = line.DepreciationAmount;
            ws.Cell(row, 6).Value = line.AccumulatedDepreciation;
            ws.Cell(row, 7).Value = line.ClosingNbv;
            ws.Cell(row, 8).Value = line.IsPosted ? "Oui" : "Non";
            ApplyAmountFormat(ws, row, 2, 7);
            row++;
        }

        ws.Columns().AdjustToContents();
    }

    public byte[] ExportDepreciationReportToExcel(
        IReadOnlyList<FixedAssetScheduleDto> schedules,
        int fiscalYear,
        int fiscalYearStartMonth = 1,
        string fiscalYearLabelFormat = "N/N+1")
    {
        using var wb = new XLWorkbook();
        // Libellé d'exercice (N en civil = bit-à-bit identique à l'existant ; N/N+1 en décalé).
        // Le nom de feuille Excel n'accepte pas les caractères interdits (notamment « / » du
        // libellé N/N+1) : on les remplace par un tiret. En civil, aucun remplacement (parité).
        var sheetName = SanitizeSheetName($"Dotations {_fiscalYearResolver.FiscalYearLabel(fiscalYear, fiscalYearStartMonth, fiscalYearLabelFormat)}");
        var ws = wb.Worksheets.Add(sheetName);

        var headers = new[]
        {
            "N° inventaire", "Désignation", "Date acquisition", "Date mise en service",
            "Valeur origine", "Taux %", "Durée (ans)", "Base amortissable",
            "Annuité normale", "Amort. antérieurs", "Dotation exercice", "Cumul", "VNC fin"
        };
        for (var c = 0; c < headers.Length; c++)
            ws.Cell(1, c + 1).Value = headers[c];
        StyleHeader(ws, 1, headers.Length);

        var row = 2;
        foreach (var schedule in schedules)
        {
            // Sélection de la ligne par clé d'exercice (P2) — inchangée : en civil la clé = année civile,
            // en exercice décalé la clé est l'année de début (résolution correcte de la ligne d'exercice).
            var line = schedule.Lines.FirstOrDefault(l => l.FiscalYear == fiscalYear);
            if (line is null)
                continue;

            ws.Cell(row, 1).Value = schedule.InventoryNumber;
            ws.Cell(row, 2).Value = schedule.Label;
            ws.Cell(row, 3).Value = schedule.AcquisitionDate;
            ws.Cell(row, 3).Style.DateFormat.Format = "dd/MM/yyyy";
            if (schedule.InServiceDate.HasValue)
            {
                ws.Cell(row, 4).Value = schedule.InServiceDate.Value;
                ws.Cell(row, 4).Style.DateFormat.Format = "dd/MM/yyyy";
            }
            ws.Cell(row, 5).Value = schedule.TotalCapitalizedCost;
            ws.Cell(row, 6).Value = schedule.DepreciationRatePercent;
            ws.Cell(row, 7).Value = schedule.UsefulLifeYears;
            ws.Cell(row, 8).Value = schedule.DepreciableBase;
            ws.Cell(row, 9).Value = line.NormalAnnualAmount;
            ws.Cell(row, 10).Value = line.PriorAccumulatedDepreciation;
            ws.Cell(row, 11).Value = line.DepreciationAmount;
            ws.Cell(row, 12).Value = line.AccumulatedDepreciation;
            ws.Cell(row, 13).Value = line.ClosingNbv;
            ApplyAmountFormat(ws, row, 5, 13);
            row++;
        }

        ws.Columns().AdjustToContents();
        return WorkbookToBytes(wb);
    }

    public byte[] ExportAmortizationReportToExcel(
        AmortizationReportResponse report,
        int fiscalYearStartMonth = 1,
        string fiscalYearLabelFormat = "N/N+1")
    {
        using var wb = new XLWorkbook();
        AddAmortizationReportMainSheet(wb, report, fiscalYearStartMonth, fiscalYearLabelFormat);
        AddAmortizationReportSummarySheet(wb, report, fiscalYearStartMonth, fiscalYearLabelFormat);
        AddAmortizationReportInfoSheet(wb, report);
        return WorkbookToBytes(wb);
    }

    private void AddAmortizationReportMainSheet(
        XLWorkbook wb,
        AmortizationReportResponse report,
        int fiscalYearStartMonth,
        string fiscalYearLabelFormat)
    {
        var ws = wb.Worksheets.Add("Tableau amortissements");
        var fy = report.Header.FiscalYear;
        // Bornes d'exercice (début/fin) via le résolveur : en civil, 31/12/N (bit-à-bit identique) ;
        // en exercice décalé, fin d'exercice réelle (ex. 30/06/N+1).
        var priorEnd = _fiscalYearResolver.FiscalYearEndDateTime(fy - 1, fiscalYearStartMonth);
        var currentEnd = _fiscalYearResolver.FiscalYearEndDateTime(fy, fiscalYearStartMonth);
        var label = _fiscalYearResolver.FiscalYearLabel(fy, fiscalYearStartMonth, fiscalYearLabelFormat);

        ws.Cell(1, 1).Value = report.Header.CompanyName;
        ws.Cell(2, 1).Value = "TABLEAU DES AMORTISSEMENTS";
        ws.Cell(2, 1).Style.Font.Bold = true;
        // En exercice civil, l'en-tête reste strictement « Exercice du … au … » (parité). En exercice
        // décalé, le libellé N/N+1 est affiché en tête (décision D2).
        var exerciseHeader = fiscalYearStartMonth == 1
            ? $"Exercice du {report.Header.PeriodStart:dd/MM/yyyy} au {report.Header.PeriodEnd:dd/MM/yyyy}"
            : $"Exercice {label} — du {report.Header.PeriodStart:dd/MM/yyyy} au {report.Header.PeriodEnd:dd/MM/yyyy}";
        ws.Cell(3, 1).Value = exerciseHeader;

        var headers = new[]
        {
            "Code immobilisation", "Désignation", "Date acquisition", "Valeur origine",
            "Durée (ans)", "Mode", $"Amort. antérieurs {priorEnd:dd/MM/yyyy}",
            "Dotation calculée", "Dotation comptabilisée", $"Fin exercice {currentEnd:dd/MM/yyyy}",
            $"VNC {currentEnd:dd/MM/yyyy}", "Statut compta"
        };
        var headerRow = 5;
        for (var c = 0; c < headers.Length; c++)
            ws.Cell(headerRow, c + 1).Value = headers[c];
        StyleHeader(ws, headerRow, headers.Length);

        var row = headerRow + 1;
        foreach (var group in report.Groups)
        {
            ws.Cell(row, 1).Value = group.GroupLabel;
            ws.Range(row, 1, row, headers.Length).Merge();
            ws.Cell(row, 1).Style.Font.Bold = true;
            row++;

            foreach (var asset in group.Rows)
            {
                ws.Cell(row, 1).Value = asset.AssetAccountNumber;
                ws.Cell(row, 2).Value = asset.Label;
                ws.Cell(row, 3).Value = asset.AcquisitionDate;
                ws.Cell(row, 3).Style.DateFormat.Format = "dd/MM/yyyy";
                ws.Cell(row, 4).Value = asset.OriginValue;
                ws.Cell(row, 5).Value = asset.UsefulLifeYears;
                ws.Cell(row, 6).Value = asset.DepreciationMethod.ToString();
                ws.Cell(row, 7).Value = asset.PriorAccumulatedDepreciation;
                ws.Cell(row, 8).Value = asset.DotationCalculeeExercice;
                ws.Cell(row, 9).Value = asset.DotationComptabiliseeExercice;
                ws.Cell(row, 10).Value = asset.EndOfYearAccumulatedDepreciation;
                ws.Cell(row, 11).Value = asset.EndOfYearNetBookValue;
                ws.Cell(row, 12).Value = asset.PostingStatus.ToString();
                ApplyAmountFormat(ws, row, 4, 11);
                row++;
            }

            ws.Cell(row, 1).Value = $"Total {group.GroupLabel}";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 4).Value = group.Subtotal.OriginValue;
            ws.Cell(row, 7).Value = group.Subtotal.PriorAccumulatedDepreciation;
            ws.Cell(row, 8).Value = group.Subtotal.DotationCalculeeExercice;
            ws.Cell(row, 9).Value = group.Subtotal.DotationComptabiliseeExercice;
            ws.Cell(row, 10).Value = group.Subtotal.EndOfYearAccumulatedDepreciation;
            ws.Cell(row, 11).Value = group.Subtotal.EndOfYearNetBookValue;
            ApplyAmountFormat(ws, row, 4, 11);
            ws.Range(row, 1, row, headers.Length).Style.Font.Bold = true;
            row++;
        }

        ws.Cell(row, 1).Value = "TOTAL GÉNÉRAL";
        ws.Cell(row, 4).Value = report.GrandTotal.OriginValue;
        ws.Cell(row, 7).Value = report.GrandTotal.PriorAccumulatedDepreciation;
        ws.Cell(row, 8).Value = report.GrandTotal.DotationCalculeeExercice;
        ws.Cell(row, 9).Value = report.GrandTotal.DotationComptabiliseeExercice;
        ws.Cell(row, 10).Value = report.GrandTotal.EndOfYearAccumulatedDepreciation;
        ws.Cell(row, 11).Value = report.GrandTotal.EndOfYearNetBookValue;
        ApplyAmountFormat(ws, row, 4, 11);
        ws.Range(row, 1, row, headers.Length).Style.Font.Bold = true;
        ws.Range(row, 1, row, headers.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A8A");
        ws.Range(row, 1, row, headers.Length).Style.Font.FontColor = XLColor.White;

        ws.Columns().AdjustToContents();
    }

    private void AddAmortizationReportSummarySheet(
        XLWorkbook wb,
        AmortizationReportResponse report,
        int fiscalYearStartMonth,
        string fiscalYearLabelFormat)
    {
        var ws = wb.Worksheets.Add("Récapitulatif");
        var fy = report.Header.FiscalYear;
        var priorEnd = _fiscalYearResolver.FiscalYearEndDateTime(fy - 1, fiscalYearStartMonth);
        var currentEnd = _fiscalYearResolver.FiscalYearEndDateTime(fy, fiscalYearStartMonth);
        var label = _fiscalYearResolver.FiscalYearLabel(fy, fiscalYearStartMonth, fiscalYearLabelFormat);

        ws.Cell(1, 1).Value = "RÉCAPITULATIF PAR NATURE D'IMMOBILISATIONS";
        ws.Cell(1, 1).Style.Font.Bold = true;

        var headers = new[]
        {
            "Nature", "Valeur origine", $"Amort. cumulés {priorEnd:dd/MM/yyyy}",
            $"Dotation exercice {label}", $"VNC {currentEnd:dd/MM/yyyy}"
        };
        var headerRow = 3;
        for (var c = 0; c < headers.Length; c++)
            ws.Cell(headerRow, c + 1).Value = headers[c];
        StyleHeader(ws, headerRow, headers.Length);

        var row = headerRow + 1;
        foreach (var summary in report.SummaryByNature)
        {
            ws.Cell(row, 1).Value = summary.NatureLabel;
            ws.Cell(row, 2).Value = summary.OriginValue;
            ws.Cell(row, 3).Value = summary.PriorAccumulatedDepreciation;
            ws.Cell(row, 4).Value = summary.DotationCalculeeExercice;
            ws.Cell(row, 5).Value = summary.EndOfYearNetBookValue;
            ApplyAmountFormat(ws, row, 2, 5);
            row++;
        }

        ws.Cell(row, 1).Value = "TOTAL GÉNÉRAL";
        ws.Cell(row, 2).Value = report.GrandTotal.OriginValue;
        ws.Cell(row, 3).Value = report.GrandTotal.PriorAccumulatedDepreciation;
        ws.Cell(row, 4).Value = report.GrandTotal.DotationCalculeeExercice;
        ws.Cell(row, 5).Value = report.GrandTotal.EndOfYearNetBookValue;
        ApplyAmountFormat(ws, row, 2, 5);
        ws.Range(row, 1, row, headers.Length).Style.Font.Bold = true;
        ws.Columns().AdjustToContents();
    }

    private static void AddAmortizationReportInfoSheet(XLWorkbook wb, AmortizationReportResponse report)
    {
        var ws = wb.Worksheets.Add("Informations");
        ws.Cell(1, 1).Value = "INFORMATIONS";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(3, 1).Value = "Linéaire : dotation constante sur la durée d'amortissement.";
        ws.Cell(4, 1).Value = "Accéléré : application du taux majoré fiscal.";
        ws.Cell(5, 1).Value = "Intégral : dotation unique l'année de mise en service.";
        ws.Cell(7, 1).Value = $"Devise : {report.InfoBox.CurrencyCode}";
        ws.Cell(8, 1).Value = $"Édité le {report.InfoBox.GeneratedAtUtc:dd/MM/yyyy à HH:mm}";
        ws.Cell(9, 1).Value = report.InfoBox.ProductName;
        ws.Columns().AdjustToContents();
    }

    private static void StyleHeader(IXLWorksheet ws, int row, int colCount)
    {
        var range = ws.Range(row, 1, row, colCount);
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#E0F2FE");
    }

    private static void ApplyAmountFormat(IXLWorksheet ws, int row, int fromCol, int toCol)
    {
        for (var c = fromCol; c <= toCol; c++)
            ws.Cell(row, c).Style.NumberFormat.Format = "#,##0.000";
    }

    private static byte[] WorkbookToBytes(XLWorkbook wb)
    {
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// Nettoie un nom de feuille des caractères interdits par Excel (\ / ? * [ ] :). En exercice
    /// civil, le libellé ne contient que des chiffres → aucun remplacement (sortie bit-à-bit
    /// identique) ; en exercice décalé, le « / » du libellé N/N+1 devient un tiret.
    /// </summary>
    private static string SanitizeSheetName(string name)
        => name.Replace('\\', '-').Replace('/', '-').Replace('?', '-')
               .Replace('*', '-').Replace('[', '-').Replace(']', '-').Replace(':', '-');
}
