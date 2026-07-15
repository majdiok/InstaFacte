using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Service for generating XML documents.
/// </summary>
public interface IXmlService
{
    /// <summary>
    /// Generates an XML representation of an invoice.
    /// </summary>
    Task<string> GenerateInvoiceXmlAsync(Invoice invoice, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validates an invoice XML against the schema.
    /// </summary>
    Task<bool> ValidateInvoiceXmlAsync(string xml, CancellationToken cancellationToken = default);
}
