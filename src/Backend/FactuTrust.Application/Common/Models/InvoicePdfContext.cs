using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Common.Models;

/// <summary>
/// Data required to render a sales invoice PDF (issuer branding, optional source quote,
/// and for a credit note the number of the invoice it rectifies).
/// </summary>
public sealed record InvoicePdfContext(
    Invoice Invoice,
    Company? Issuer,
    string? SourceQuoteNumber,
    string? LinkedInvoiceNumber = null);
