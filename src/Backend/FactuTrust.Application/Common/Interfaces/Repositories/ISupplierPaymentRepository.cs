using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

/// <summary>
/// Repository interface for SupplierPayment aggregate.
/// </summary>
public interface ISupplierPaymentRepository : IRepository<SupplierPayment>
{
    /// <summary>
    /// Loads supplier payments by id with <see cref="SupplierPayment.SupplierInvoice"/> for list enrichment.
    /// </summary>
    Task<IReadOnlyList<SupplierPayment>> GetByIdsWithInvoiceAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all payments for a supplier invoice, ordered by payment date descending.
    /// </summary>
    Task<IReadOnlyList<SupplierPayment>> GetBySupplierInvoiceIdAsync(Guid supplierInvoiceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all supplier payments within a date range (payment date), with invoice and supplier loaded.
    /// </summary>
    Task<IReadOnlyList<SupplierPayment>> GetByDateRangeAsync(
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total paid amount per supplier invoice for the given invoice IDs.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, decimal>> GetTotalPaidBySupplierInvoiceIdsAsync(
        IEnumerable<Guid> supplierInvoiceIds,
        CancellationToken cancellationToken = default);
}
