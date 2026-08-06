using FactuTrust.Application.Common.Enums;
using FactuTrust.Domain.Services.Payroll;

namespace FactuTrust.Application.Features.Payroll.Reports;

/// <summary>
/// Fabriques partagées des états de contrôle paie (libellés de période, arrondi millime,
/// libellés des comptes de la ventilation). Centralise pour garantir des sorties homogènes
/// entre le livre de paie, le journal de paie et leurs exports.
/// </summary>
internal static class PayrollReportHelpers
{
    /// <summary>
    /// Type source de l'écriture comptable de paie. Doit rester aligné sur
    /// <c>AccountingService.SourcePayrollRun</c> (Infrastructure, non référençable depuis Application).
    /// </summary>
    public const string PayrollRunSourceType = "PayrollRun";

    private static readonly string[] MonthNamesFr =
    {
        "", "Janvier", "Février", "Mars", "Avril", "Mai", "Juin",
        "Juillet", "Août", "Septembre", "Octobre", "Novembre", "Décembre"
    };

    public static string MonthName(int month) =>
        month is >= 1 and <= 12 ? MonthNamesFr[month] : $"M{month}";

    /// <summary>« Mars 2026 » pour un mois isolé, « Janvier à Mars 2026 » pour une plage.</summary>
    public static string PeriodLabel(int year, int fromMonth, int toMonth) =>
        fromMonth == toMonth
            ? $"{MonthName(fromMonth)} {year}"
            : $"{MonthName(fromMonth)} à {MonthName(toMonth)} {year}";

    /// <summary>Arrondi millime (convention comptable tunisienne), appliqué à chaque agrégation.</summary>
    public static decimal Round(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);

    /// <summary>Extension et type MIME du fichier produit, alignés sur les exports comptables.</summary>
    public static (string Extension, string ContentType) FileMeta(AccountingExportFormat format) => format switch
    {
        AccountingExportFormat.Excel => ("xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"),
        AccountingExportFormat.Pdf => ("pdf", "application/pdf"),
        _ => ("csv", "text/csv; charset=utf-8")
    };

    /// <summary>
    /// Libellé lisible d'un compte de la ventilation de paie. Les comptes auxiliaires 421xxxx
    /// (option « comptes auxiliaires salariés ») retombent sur le libellé du compte collectif.
    /// </summary>
    public static string AccountLabel(string accountNumber)
    {
        if (string.IsNullOrWhiteSpace(accountNumber))
            return string.Empty;

        if (accountNumber.StartsWith(PayrollJournalEntryBuilder.PersonnelPayableAccount, StringComparison.Ordinal))
            return "Personnel — rémunérations dues";

        return accountNumber switch
        {
            PayrollJournalEntryBuilder.SalaryAccount => "Charges de personnel",
            PayrollJournalEntryBuilder.EmployerChargesAccount => "Charges sociales de l'employeur",
            PayrollJournalEntryBuilder.StateWithholdingAccount => "État — retenues et taxes sur salaires",
            PayrollJournalEntryBuilder.SocialOrgAccount => "Organismes sociaux",
            PayrollJournalEntryBuilder.AdvancesAccount => "Personnel — avances et acomptes",
            _ => string.Empty
        };
    }
}
