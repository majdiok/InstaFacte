using FactuTrust.Application.Common.Enums;
using FactuTrust.Application.DTOs;

namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Exports tabulaires (CSV / Excel) des états de contrôle paie. Le PDF reste porté par
/// <see cref="IPdfService"/>, comme pour les états comptables.
/// </summary>
public interface IPayrollReportExportService
{
    byte[] ExportPayrollBookToCsv(PayrollBookDto book);

    byte[] ExportPayrollBookToExcel(PayrollBookDto book);

    byte[] ExportPayrollJournalToCsv(PayrollJournalDto journal, PayrollJournalView view);

    byte[] ExportPayrollJournalToExcel(PayrollJournalDto journal, PayrollJournalView view);
}
