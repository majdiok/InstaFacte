using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.Invoices.Queries;

/// <summary>
/// Query to export an invoice as PDF. <paramref name="TemplateKey"/> permet une surcharge ponctuelle
/// du modèle visuel (sinon : préférence du tenant, puis modèle par défaut).
/// </summary>
public sealed record ExportInvoicePdfQuery(Guid InvoiceId, string? TemplateKey = null) : IRequest<Result<InvoicePdfResult>>;

/// <summary>
/// Result containing the PDF data.
/// </summary>
public sealed record InvoicePdfResult(byte[] Content, string FileName, string ContentType);

/// <summary>
/// Handler for ExportInvoicePdfQuery.
/// </summary>
public sealed class ExportInvoicePdfQueryHandler : IRequestHandler<ExportInvoicePdfQuery, Result<InvoicePdfResult>>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IPdfService _pdfService;
    private readonly IInvoicePdfContextLoader _invoicePdfContextLoader;
    private readonly IAuditService _auditService;
    private readonly IDocumentTemplateResolver _templateResolver;

    public ExportInvoicePdfQueryHandler(
        IInvoiceRepository invoiceRepository,
        IPdfService pdfService,
        IInvoicePdfContextLoader invoicePdfContextLoader,
        IAuditService auditService,
        IDocumentTemplateResolver templateResolver)
    {
        _invoiceRepository = invoiceRepository;
        _pdfService = pdfService;
        _invoicePdfContextLoader = invoicePdfContextLoader;
        _auditService = auditService;
        _templateResolver = templateResolver;
    }

    public async Task<Result<InvoicePdfResult>> Handle(ExportInvoicePdfQuery request, CancellationToken cancellationToken)
    {
        var invoice = await _invoiceRepository.GetByIdWithLinesAsync(request.InvoiceId, cancellationToken);

        if (invoice is null)
            return Result.Failure<InvoicePdfResult>(Error.NotFound("Facture", request.InvoiceId));

        var documentType = invoice.IsCreditNote ? PrintableDocumentType.CreditNote : PrintableDocumentType.SalesInvoice;
        var templateKey = await _templateResolver.ResolveAsync(documentType, request.TemplateKey, cancellationToken);

        var pdfContext = await _invoicePdfContextLoader.LoadAsync(invoice, cancellationToken);
        var pdfBytes = await _pdfService.GenerateInvoicePdfAsync(pdfContext, templateKey, cancellationToken);

        var fileName = $"Facture_{invoice.Number.Value}.pdf";

        await _auditService.LogAsync(
            AuditActions.Export.Pdf,
            "Invoice",
            invoice.Id,
            cancellationToken: cancellationToken);

        return Result.Success(new InvoicePdfResult(pdfBytes, fileName, "application/pdf"));
    }
}
