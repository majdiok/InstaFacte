using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Common.Models;

/// <summary>
/// Data required to render a sales invoice PDF (issuer branding, optional source quote).
/// </summary>
public sealed record InvoicePdfContext(
    Invoice Invoice,
    Company? Issuer,
    string? SourceQuoteNumber);
