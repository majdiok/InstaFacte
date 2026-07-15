using FactuTrust.Application.Common.Models;
using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Loads issuer company and related display data for invoice PDF generation.
/// </summary>
public interface IInvoicePdfContextLoader
{
    Task<InvoicePdfContext> LoadAsync(Invoice invoice, CancellationToken cancellationToken = default);
}
