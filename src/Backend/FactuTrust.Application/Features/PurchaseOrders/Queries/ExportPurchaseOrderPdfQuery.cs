using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.PurchaseOrders.Queries;

/// <summary>
/// Query to export a purchase order as PDF. <paramref name="TemplateKey"/> permet une surcharge du modèle.
/// </summary>
public sealed record ExportPurchaseOrderPdfQuery(Guid PurchaseOrderId, string? TemplateKey = null) : IRequest<Result<PurchaseOrderPdfResult>>;

/// <summary>
/// Result containing the PDF data.
/// </summary>
public sealed record PurchaseOrderPdfResult(byte[] Content, string FileName, string ContentType);

/// <summary>
/// Handler for ExportPurchaseOrderPdfQuery.
/// </summary>
public sealed class ExportPurchaseOrderPdfQueryHandler : IRequestHandler<ExportPurchaseOrderPdfQuery, Result<PurchaseOrderPdfResult>>
{
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly IPdfService _pdfService;
    private readonly IAuditService _auditService;
    private readonly IDocumentTemplateResolver _templateResolver;
    private readonly ICompanyRepository _companyRepository;

    public ExportPurchaseOrderPdfQueryHandler(
        IPurchaseOrderRepository purchaseOrderRepository,
        IPdfService pdfService,
        IAuditService auditService,
        IDocumentTemplateResolver templateResolver,
        ICompanyRepository companyRepository)
    {
        _purchaseOrderRepository = purchaseOrderRepository;
        _pdfService = pdfService;
        _auditService = auditService;
        _templateResolver = templateResolver;
        _companyRepository = companyRepository;
    }

    public async Task<Result<PurchaseOrderPdfResult>> Handle(ExportPurchaseOrderPdfQuery request, CancellationToken cancellationToken)
    {
        var po = await _purchaseOrderRepository.GetByIdWithLinesAsync(request.PurchaseOrderId, cancellationToken);

        if (po is null)
            return Result.Failure<PurchaseOrderPdfResult>(Error.NotFound("PurchaseOrder", request.PurchaseOrderId));

        var templateKey = await _templateResolver.ResolveAsync(PrintableDocumentType.PurchaseOrder, request.TemplateKey, cancellationToken);
        var issuer = await _companyRepository.GetDefaultAsync(cancellationToken);
        var pdfBytes = await _pdfService.GeneratePurchaseOrderPdfAsync(po, issuer, templateKey, cancellationToken);

        var fileName = $"BC_{po.Number.Value}.pdf";

        await _auditService.LogAsync(
            AuditActions.PurchaseOrder.Exported,
            "PurchaseOrder",
            po.Id,
            cancellationToken: cancellationToken);

        return Result.Success(new PurchaseOrderPdfResult(pdfBytes, fileName, "application/pdf"));
    }
}
