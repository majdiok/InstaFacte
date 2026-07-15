using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Common.Interfaces.Repositories;

/// <summary>
/// Repository interface for Payment aggregate.
/// </summary>
public interface IPaymentRepository : IRepository<Payment>
{
    /// <summary>
    /// Gets all payments for an invoice, ordered by payment date descending.
    /// </summary>
    Task<IReadOnlyList<Payment>> GetByInvoiceIdAsync(Guid invoiceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total paid amount per invoice (sum of non-refunded payments) for the given invoice IDs.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, decimal>> GetTotalPaidByInvoiceIdsAsync(
        IEnumerable<Guid> invoiceIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all client payments within a date range (payment date), with invoice and client loaded.
    /// </summary>
    Task<IReadOnlyList<Payment>> GetByDateRangeAsync(
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default);

    /// <summary>Client payments report rows (projected SQL, no full entity graph).</summary>
    Task<IReadOnlyList<ClientPaymentReportSourceDto>> GetClientPaymentReportSourcesAsync(
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default);

    /// <summary>Retenues subies par mois (date de paiement) pour une année.</summary>
    Task<IReadOnlyList<(int Month, decimal TotalSubie, int PaymentCount)>> GetClientWithholdingAggregatesByYearAsync(
        int year,
        CancellationToken cancellationToken = default);

    /// <summary>Client withholdings report aggregates grouped by client for a payment date range.</summary>
    Task<IReadOnlyList<ClientWithholdingReportRowDto>> GetClientWithholdingReportRowsAsync(
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default);
}
