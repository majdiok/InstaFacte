namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Server-side numbering for internal supplier invoice references (FS-YYYY-NNNNNN).
/// </summary>
public interface ISupplierInvoiceNumberService
{
  Task<string> PreviewNextAsync(DateTime invoiceDate, CancellationToken cancellationToken = default);

  Task<string> ReserveNextAsync(DateTime invoiceDate, CancellationToken cancellationToken = default);

  Task<bool> IsAvailableAsync(string invoiceNumber, CancellationToken cancellationToken = default);
}
