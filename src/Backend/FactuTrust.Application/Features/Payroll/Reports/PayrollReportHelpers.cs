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
    /// Libellé lisible d'un compte de la ventilation de paie. Les comptes auxiliaires 425xxxx
    /// (option « comptes auxiliaires salariés ») retombent sur le libellé du compte collectif.
    /// </summary>
    /// <remarks>
    /// La table couvre les deux profils d'imputation. Sans les comptes du profil SCE, la colonne
    /// « Libellé » du journal de paie et de ses exports se vidait dès la bascule — les montants
    /// s'affichaient en face d'un compte sans nom.
    /// </remarks>
    public static string AccountLabel(string accountNumber)
    {
        if (string.IsNullOrWhiteSpace(accountNumber))
            return string.Empty;

        if (accountNumber.StartsWith(PayrollJournalEntryBuilder.PersonnelPayableAccount, StringComparison.Ordinal))
            return "Personnel — rémunérations dues";

        return accountNumber switch
        {
            // ── Charges (classe 6), communes aux deux profils ──
            PayrollJournalEntryBuilder.SalaryAccount => "Charges de personnel",
            PayrollJournalEntryBuilder.EmployerChargesAccount => "Charges sociales de l'employeur",
            PayrollJournalEntryBuilder.IndemnityAccount => "Indemnités de rupture et soldes de tout compte",

            // ── Charges ventilées du profil SCE ──
            PayrollJournalEntryBuilder.TfpExpenseAccount => "TFP",
            PayrollJournalEntryBuilder.FoprolosExpenseAccount => "FOPROLOS",
            PayrollJournalEntryBuilder.TerminationIndemnityAccount => "Indemnités de préavis et de licenciement",
            PayrollJournalEntryBuilder.InKindBenefitExpenseAccount => "Avantages en nature",
            "6401" => "Heures supplémentaires",
            "6402" => "Primes",
            "6403" => "Gratifications",

            // ── Dettes et créances (classe 4) ──
            PayrollJournalEntryBuilder.StateWithholdingAccount => "État — retenues et taxes sur salaires",
            PayrollJournalEntryBuilder.PayrollTaxesPayableAccount => "État — autres impôts et taxes sur rémunérations",
            PayrollJournalEntryBuilder.SocialOrgAccount => "Organismes sociaux",
            PayrollJournalEntryBuilder.SocialFundEmployerPayableAccount => "Organismes sociaux — charges à payer",
            PayrollJournalEntryBuilder.AdvancesAccount => "Personnel — avances et acomptes",
            PayrollJournalEntryBuilder.OtherDeductionsPayableAccount => "Personnel — autres charges à payer",
            "4386" => "État — autres charges à payer",
            "421.1" => "Personnel — prêts en cours",
            "427" => "Personnel — oppositions et saisies",
            "428.1" => "Personnel — mutuelle et caisses complémentaires",
            "428.2" => "Personnel — tickets restaurant (part salariale)",
            _ => string.Empty
        };
    }
}
