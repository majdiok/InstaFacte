using FactuTrust.Application.DTOs;

namespace FactuTrust.Application.Common.Interfaces.Services;

public interface IAccountingExportService
{
    byte[] ExportJournalToCsv(IReadOnlyList<JournalEntryDto> entries);
    byte[] ExportLedgerToCsv(IReadOnlyList<LedgerRowDto> rows);
    byte[] ExportBalanceToCsv(IReadOnlyList<BalanceRowDto> rows);
    byte[] ExportJournalSummaryToCsv(JournalSummaryDto summary);
    byte[] ExportJournalToExcel(IReadOnlyList<JournalEntryDto> entries);
    byte[] ExportJournalSummaryToExcel(JournalSummaryDto summary);
    byte[] ExportLedgerToExcel(IReadOnlyList<LedgerRowDto> rows, string accountNumber);
    byte[] ExportGeneralLedgerToCsv(GeneralLedgerDto ledger);
    byte[] ExportGeneralLedgerToExcel(GeneralLedgerDto ledger);
    byte[] ExportBalanceToExcel(IReadOnlyList<BalanceRowDto> rows);
    byte[] ExportDetailedBalanceToCsv(DetailedBalanceDto balance);
    byte[] ExportDetailedBalanceToExcel(DetailedBalanceDto balance);
    byte[] ExportPeriodicBalanceToCsv(PeriodicBalanceDto balance);
    byte[] ExportPeriodicBalanceToExcel(PeriodicBalanceDto balance);
    byte[] ExportAuxiliaryBalanceToCsv(IReadOnlyList<AuxiliaryBalanceRowDto> rows);
    byte[] ExportAuxiliaryBalanceToExcel(IReadOnlyList<AuxiliaryBalanceRowDto> rows);
    byte[] ExportThirdPartyLedgerToCsv(ThirdPartyLedgerDto ledger);
    byte[] ExportThirdPartyLedgerToExcel(ThirdPartyLedgerDto ledger);
    byte[] ExportBankReconciliationStatementToCsv(BankReconciliationStatementDto statement);
    byte[] ExportBankReconciliationStatementToExcel(BankReconciliationStatementDto statement);
    byte[] ExportAgingToCsv(IReadOnlyList<AgingReportRowDto> rows, string kindLabel);
    byte[] ExportAgingToExcel(IReadOnlyList<AgingReportRowDto> rows, string kindLabel);
    byte[] ExportBalanceSheetToCsv(BalanceSheetDto dto);
    byte[] ExportBalanceSheetToExcel(BalanceSheetDto dto);
    byte[] ExportIncomeStatementToCsv(IncomeStatementDto dto);
    byte[] ExportIncomeStatementToExcel(IncomeStatementDto dto);
    byte[] ExportBudgetReportToCsv(BudgetReportDto report);
    byte[] ExportBudgetReportToExcel(BudgetReportDto report);
    byte[] ExportFiscalResultToCsv(FiscalResultDeclarationDto dto);
    byte[] ExportFiscalResultToExcel(FiscalResultDeclarationDto dto);
    byte[] ExportConsolidatedLiasseToExcel(ConsolidatedLiasseDto dto);
}
