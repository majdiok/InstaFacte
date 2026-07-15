using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Application.DTOs;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface IFixedAssetRepository
{
    Task<FixedAsset?> GetByIdAsync(Guid id, bool includeSchedule = false, bool includeEvents = false, CancellationToken cancellationToken = default);
    Task<FixedAsset?> GetByInventoryNumberAsync(string inventoryNumber, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<FixedAsset> Items, int TotalCount)> SearchAsync(
        int page,
        int pageSize,
        FixedAssetStatus? status,
        Guid? categoryId,
        int? fiscalYear,
        string? search,
        CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<FixedAssetAmortizationTableRowDto> Items, int TotalCount)> SearchCurrentYearAmortizationTableAsync(
        int page,
        int pageSize,
        int fiscalYear,
        FixedAssetStatus? status,
        Guid? categoryId,
        string? search,
        CancellationToken cancellationToken = default);
    Task<AmortizationReportResponse> GetAmortizationReportAsync(
        int fiscalYear,
        AmortizationReportGroupingMode groupingMode,
        FixedAssetStatus? status,
        Guid? categoryId,
        string? search,
        string companyName,
        CancellationToken cancellationToken = default);
    Task<int> CountByYearPrefixAsync(int year, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FixedAsset>> GetBySupplierInvoiceIdAsync(Guid supplierInvoiceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FixedAsset>> GetActiveForDepreciationRunAsync(int fiscalYear, CancellationToken cancellationToken = default);
    Task<FixedAsset> AddAsync(FixedAsset entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(FixedAsset entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mise en service atomique : statut, événement InService et remplacement optionnel du tableau dans une seule transaction.
    /// </summary>
    Task<Result<FixedAsset>> PutInServiceInTransactionAsync(
        Guid id,
        DateTime inServiceDate,
        string creditAccountNumber,
        IReadOnlyList<DepreciationScheduleLine>? scheduleLinesToReplace,
        string updatedBy,
        CancellationToken cancellationToken = default);

    Task ReplaceScheduleLinesAsync(Guid fixedAssetId, IReadOnlyList<DepreciationScheduleLine> lines, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DepreciationScheduleLine>> GetUnpostedScheduleLinesForYearAsync(int fiscalYear, CancellationToken cancellationToken = default);
    Task SaveScheduleLineAsync(DepreciationScheduleLine line, CancellationToken cancellationToken = default);
}
