using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

public interface IPurchaseReceiptRepository : IRepository<PurchaseReceipt>
{
    Task<PurchaseReceipt?> GetByIdWithLinesAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PurchaseReceipt?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<PurchaseReceipt> Items, int TotalCount)> SearchAsync(
        string? searchTerm,
        PurchaseReceiptStatus? status,
        Guid? supplierId,
        Guid? purchaseOrderId,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<PurchaseReceiptListSummaryDto> GetSummaryAsync(
        string? searchTerm,
        PurchaseReceiptStatus? status,
        Guid? supplierId,
        Guid? purchaseOrderId,
        DateTime? fromDate,
        DateTime? toDate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PurchaseReceipt>> GetByPurchaseOrderIdAsync(
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsNumberAsync(string number, CancellationToken cancellationToken = default);
}
