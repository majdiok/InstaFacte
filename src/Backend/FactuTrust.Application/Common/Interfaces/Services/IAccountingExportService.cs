using FactuTrust.Application.DTOs;

namespace FactuTrust.Application.Common.Interfaces.Services;

public interface IAccountingExportService
{
    byte[] ExportJournalToCsv(IReadOnlyList<JournalEntryDto> entries);
    byte[] ExportLedgerToCsv(IReadOnlyList<LedgerRowDto> rows);
    byte[] ExportBalanceToCsv(IReadOnlyList<BalanceRowDto> rows);
    byte[] ExportJournalToExcel(IReadOnlyList<JournalEntryDto> entries);
    byte[] ExportLedgerToExcel(IReadOnlyList<LedgerRowDto> rows, string accountNumber);
    byte[] ExportBalanceToExcel(IReadOnlyList<BalanceRowDto> rows);
    byte[] ExportAuxiliaryBalanceToCsv(IReadOnlyList<AuxiliaryBalanceRowDto> rows);
    byte[] ExportThirdPartyLedgerToCsv(ThirdPartyLedgerDto ledger);
    byte[] ExportBudgetReportToCsv(BudgetReportDto report);
    byte[] ExportBudgetReportToExcel(BudgetReportDto report);
}
