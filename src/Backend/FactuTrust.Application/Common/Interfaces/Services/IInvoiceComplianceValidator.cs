using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Service for validating invoice compliance with Tunisian fiscal regulations.
/// </summary>
public interface IInvoiceComplianceValidator
{
    /// <summary>
    /// Validates an invoice draft for compliance before submission.
    /// </summary>
    /// <param name="draft">The draft to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result with detailed checks</returns>
    Task<WizardValidationResultDto> ValidateAsync(
        InvoiceDraft draft, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates an existing invoice for compliance.
    /// </summary>
    /// <param name="invoice">The invoice to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result with detailed checks</returns>
    Task<WizardValidationResultDto> ValidateInvoiceAsync(
        Invoice invoice, 
        CancellationToken cancellationToken = default);
}
