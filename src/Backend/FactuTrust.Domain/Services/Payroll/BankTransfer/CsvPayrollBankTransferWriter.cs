using System.Globalization;
using System.Text;

namespace FactuTrust.Domain.Services.Payroll.BankTransfer;

/// <summary>
/// Writer CSV standard (; UTF-8 BOM) pour virements de salaires.
/// Aligné sur les conventions DTS / exports comptables.
/// </summary>
public sealed class CsvPayrollBankTransferWriter : IPayrollBankTransferFormatWriter
{
    public PayrollBankTransferFormat Format => PayrollBankTransferFormat.StandardCsv;
    public string FileExtension => "csv";
    public string ContentType => "text/csv; charset=utf-8";

    public byte[] Write(PayrollBankTransferBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        var sb = new StringBuilder();

        // Section méta (commentaires).
        AppendMeta(sb, "Société", batch.Company?.Name ?? string.Empty);
        AppendMeta(sb, "Période", batch.PeriodLabel);
        AppendMeta(sb, "Compte débiteur RIB", batch.DebtorAccount?.Rib ?? string.Empty);
        AppendMeta(sb, "Compte débiteur IBAN", batch.DebtorAccount?.Iban ?? string.Empty);
        AppendMeta(sb, "Banque", batch.DebtorAccount?.BankName ?? string.Empty);
        AppendMeta(sb, "Date export", batch.ExportDateUtc.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        AppendMeta(sb, "Nombre virements", batch.EligibleCount.ToString(CultureInfo.InvariantCulture));
        AppendMeta(sb, "Montant total", FormatAmount(batch.TotalAmount));
        sb.AppendLine();

        // En-tête colonnes.
        sb.AppendLine("Type ligne;Matricule;Nom;Prénom;RIB;IBAN;Montant net;Libellé;CIN;CNSS;Réf saisie");

        foreach (var line in batch.Lines)
        {
            var typeLabel = line.Kind == PayrollBankTransferLineKind.GarnishmentBeneficiary
                ? "Bénéficiaire saisie"
                : "Salarié";
            sb.Append(Escape(typeLabel)).Append(';');
            sb.Append(Escape(line.EmployeeNumber)).Append(';');
            sb.Append(Escape(line.LastName)).Append(';');
            sb.Append(Escape(line.FirstName)).Append(';');
            sb.Append(Escape(line.Rib)).Append(';');
            sb.Append(Escape(line.Iban)).Append(';');
            sb.Append(FormatAmount(line.NetSalary)).Append(';');
            sb.Append(Escape(line.TransferLabel)).Append(';');
            sb.Append(Escape(line.Cin ?? string.Empty)).Append(';');
            sb.Append(Escape(line.CnssNumber ?? string.Empty)).Append(';');
            sb.Append(Escape(line.GarnishmentReference ?? string.Empty));
            sb.AppendLine();
        }

        // Ligne TOTAL : TOTAL;;;{count};;{sum};;
        sb.Append("TOTAL;;;");
        sb.Append(batch.EligibleCount.ToString(CultureInfo.InvariantCulture));
        sb.Append(";;");
        sb.Append(FormatAmount(batch.TotalAmount));
        sb.Append(";;");
        sb.AppendLine();

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    private static void AppendMeta(StringBuilder sb, string key, string value)
    {
        sb.Append("# ").Append(Escape(key)).Append(';').Append(Escape(value)).AppendLine();
    }

    private static string FormatAmount(decimal amount) =>
        amount.ToString("0.000", CultureInfo.InvariantCulture);

    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        if (value.Contains(';') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";

        return value;
    }
}
