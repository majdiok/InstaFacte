using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.SupplierInvoices.Queries;

/// <summary>
/// Exporte une facture d'achat (fournisseur) en PDF (modèle configurable, surcharge possible).
/// </summary>
public sealed record ExportSupplierInvoicePdfQuery(Guid SupplierInvoiceId, string? TemplateKey = null)
    : IRequest<Result<SupplierInvoicePdfResult>>;

public sealed record SupplierInvoicePdfResult(byte[] Content, string FileName, string ContentType);

public sealed class ExportSupplierInvoicePdfQueryHandler
    : IRequestHandler<ExportSupplierInvoicePdfQuery, Result<SupplierInvoicePdfResult>>
{
    private readonly ISupplierInvoiceRepository _repository;
    private readonly IPdfService _pdfService;
    private readonly IAuditService _auditService;
    private readonly IDocumentTemplateResolver _templateResolver;
    private readonly ICompanyRepository _companyRepository;

    public ExportSupplierInvoicePdfQueryHandler(
        ISupplierInvoiceRepository repository,
        IPdfService pdfService,
        IAuditService auditService,
        IDocumentTemplateResolver templateResolver,
        ICompanyRepository companyRepository)
    {
        _repository = repository;
        _pdfService = pdfService;
        _auditService = auditService;
        _templateResolver = templateResolver;
        _companyRepository = companyRepository;
    }

    public async Task<Result<SupplierInvoicePdfResult>> Handle(ExportSupplierInvoicePdfQuery request, CancellationToken cancellationToken)
    {
        var supplierInvoice = await _repository.GetByIdWithLinesAsync(request.SupplierInvoiceId, cancellationToken);
        if (supplierInvoice is null)
            return Result.Failure<SupplierInvoicePdfResult>(Error.NotFound("Facture d'achat", request.SupplierInvoiceId));

        var templateKey = await _templateResolver.ResolveAsync(PrintableDocumentType.SupplierInvoice, request.TemplateKey, cancellationToken);
        var issuer = await _companyRepository.GetDefaultAsync(cancellationToken);
        var pdfBytes = await _pdfService.GenerateSupplierInvoicePdfAsync(supplierInvoice, issuer, templateKey, cancellationToken);

        var safeNumber = supplierInvoice.InvoiceNumber.Replace('/', '-').Replace('\\', '-');
        var fileName = $"Facture_Achat_{safeNumber}.pdf";

        await _auditService.LogAsync(AuditActions.Export.Pdf, "SupplierInvoice", supplierInvoice.Id, cancellationToken: cancellationToken);

        return Result.Success(new SupplierInvoicePdfResult(pdfBytes, fileName, "application/pdf"));
    }
}
