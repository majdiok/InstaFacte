using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using AuditActions = FactuTrust.Domain.Entities.AuditActions;
using MediatR;

namespace FactuTrust.Application.Features.PurchaseReceipts.Queries;

public sealed record ExportPurchaseReceiptPdfQuery(Guid PurchaseReceiptId)
    : IRequest<Result<PurchaseReceiptPdfResult>>;

public sealed record PurchaseReceiptPdfResult(byte[] Content, string FileName, string ContentType);

public sealed class ExportPurchaseReceiptPdfQueryHandler
    : IRequestHandler<ExportPurchaseReceiptPdfQuery, Result<PurchaseReceiptPdfResult>>
{
    private readonly IPurchaseReceiptRepository _purchaseReceiptRepository;
    private readonly IPdfService _pdfService;
    private readonly IAuditService _auditService;

    public ExportPurchaseReceiptPdfQueryHandler(
        IPurchaseReceiptRepository purchaseReceiptRepository,
        IPdfService pdfService,
        IAuditService auditService)
    {
        _purchaseReceiptRepository = purchaseReceiptRepository;
        _pdfService = pdfService;
        _auditService = auditService;
    }

    public async Task<Result<PurchaseReceiptPdfResult>> Handle(
        ExportPurchaseReceiptPdfQuery request,
        CancellationToken cancellationToken)
    {
        var receipt = await _purchaseReceiptRepository.GetByIdWithDetailsAsync(
            request.PurchaseReceiptId, cancellationToken);

        if (receipt is null)
            return Result.Failure<PurchaseReceiptPdfResult>(
                Error.NotFound("PurchaseReceipt", request.PurchaseReceiptId));

        var pdfBytes = await _pdfService.GeneratePurchaseReceiptPdfAsync(receipt, cancellationToken);
        var fileName = $"BR_{receipt.Number.Value}.pdf";

        await _auditService.LogAsync(
            AuditActions.PurchaseReceipt.Exported,
            "PurchaseReceipt",
            receipt.Id,
            newValues: new { Number = receipt.Number.Value },
            cancellationToken: cancellationToken);

        return Result.Success(new PurchaseReceiptPdfResult(pdfBytes, fileName, "application/pdf"));
    }
}
