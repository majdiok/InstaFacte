using FactuTrust.Application.DTOs;

namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Service that performs atomic quote-to-invoice conversion in a single transaction.
/// Ensures no partial state (e.g. invoice without quote marked as converted).
/// </summary>
public interface IQuoteToInvoiceConversionService
{
    /// <summary>
    /// Converts an accepted quote to an invoice atomically.
    /// </summary>
    /// <param name="quoteId">Quote ID</param>
    /// <param name="options">Optional overrides (dates, reference, etc.)</param>
    /// <param name="userId">Current user ID for audit</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created invoice ID, or failure</returns>
    Task<Domain.Common.Result<Guid>> ConvertAsync(
        Guid quoteId,
        ConvertQuoteToInvoiceDto? options,
        string userId,
        CancellationToken cancellationToken = default);
}
