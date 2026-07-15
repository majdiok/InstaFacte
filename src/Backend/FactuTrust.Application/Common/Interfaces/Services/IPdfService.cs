using FactuTrust.Application.Common.Models;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Entities;

namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Service for generating PDF documents.
/// </summary>
public interface IPdfService
{
    /// <summary>Génère le PDF de la déclaration mensuelle des impôts (toutes les cases).</summary>
    Task<byte[]> GenerateVatDeclarationPdfAsync(VatDeclarationDto declaration, string companyName, CancellationToken cancellationToken = default);

    /// <summary>Génère le PDF de la liasse NCT (bilan, résultat, flux, capitaux, notes).</summary>
    Task<byte[]> GenerateNctLiassePdfAsync(NctFinancialStatementsDto statements, string companyName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a PDF for a sales invoice (full layout with issuer branding).
    /// </summary>
    Task<byte[]> GenerateInvoicePdfAsync(InvoicePdfContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Génère le PDF d'une facture en appliquant un modèle visuel donné. Si <paramref name="templateKey"/>
    /// est le modèle par défaut (ou null/inconnu), le rendu historique est utilisé (zéro régression).
    /// </summary>
    Task<byte[]> GenerateInvoicePdfAsync(InvoicePdfContext context, string? templateKey, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Generates a PDF for multiple invoices (report).
    /// </summary>
    /// <param name="invoices">The invoices to include in the report.</param>
    /// <param name="fromDate">Optional start date for the period (displayed in header).</param>
    /// <param name="toDate">Optional end date for the period (displayed in header).</param>
    /// <param name="company">Optional company for branding in the report header.</param>
    /// <param name="clientName">Optional client name when report is filtered by client.</param>
    Task<byte[]> GenerateInvoiceReportPdfAsync(
        IEnumerable<Invoice> invoices,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        Company? company = null,
        string? clientName = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Generates a PDF for multiple delivery notes (report).
    /// </summary>
    Task<byte[]> GenerateDeliveryNoteReportPdfAsync(
        IEnumerable<DeliveryNote> deliveryNotes,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        Company? company = null,
        string? clientName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a PDF for a quote (devis).
    /// </summary>
    Task<byte[]> GenerateQuotePdfAsync(Quote quote, CancellationToken cancellationToken = default);

    /// <summary>Génère le PDF d'un devis avec un modèle visuel donné (défaut/inconnu = rendu historique).</summary>
    Task<byte[]> GenerateQuotePdfAsync(Quote quote, Company? issuer, string? templateKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a PDF for a purchase order (bon de commande fournisseur).
    /// </summary>
    Task<byte[]> GeneratePurchaseOrderPdfAsync(PurchaseOrder purchaseOrder, CancellationToken cancellationToken = default);

    /// <summary>Génère le PDF d'un bon de commande avec un modèle visuel donné (défaut/inconnu = rendu historique).</summary>
    Task<byte[]> GeneratePurchaseOrderPdfAsync(PurchaseOrder purchaseOrder, Company? issuer, string? templateKey, CancellationToken cancellationToken = default);

    /// <summary>Génère le PDF individuel d'un bon de livraison avec un modèle visuel.</summary>
    Task<byte[]> GenerateDeliveryNotePdfAsync(DeliveryNote deliveryNote, Company? issuer, string? templateKey, CancellationToken cancellationToken = default);

    /// <summary>Génère le PDF d'une facture d'achat (fournisseur) avec un modèle visuel.</summary>
    Task<byte[]> GenerateSupplierInvoicePdfAsync(SupplierInvoice supplierInvoice, Company? issuer, string? templateKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a PDF for a stock transfer between warehouses.
    /// </summary>
    Task<byte[]> GenerateStockTransferPdfAsync(StockTransfer stockTransfer, CancellationToken cancellationToken = default);

    /// <summary>Génère le PDF d'un bulletin de paie.</summary>
    Task<byte[]> GeneratePayslipPdfAsync(PayslipDetailDto payslip, string companyName, CancellationToken cancellationToken = default);
}
