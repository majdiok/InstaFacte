using FactuTrust.Application.DTOs;

namespace FactuTrust.Application.Common.Interfaces.Services;

public interface IFixedAssetExportService
{
    byte[] ExportScheduleToExcel(FixedAssetScheduleDto schedule);
    byte[] ExportDepreciationReportToExcel(IReadOnlyList<FixedAssetScheduleDto> schedules, int fiscalYear);
    byte[] ExportAmortizationReportToExcel(AmortizationReportResponse report);
}