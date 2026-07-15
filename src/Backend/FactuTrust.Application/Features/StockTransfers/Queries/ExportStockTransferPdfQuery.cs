using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using MediatR;
using AuditActions = FactuTrust.Domain.Entities.AuditActions;

namespace FactuTrust.Application.Features.StockTransfers.Queries;

public sealed record ExportStockTransferPdfQuery(Guid StockTransferId) : IRequest<Result<StockTransferPdfResult>>;

public sealed record StockTransferPdfResult(byte[] Content, string FileName, string ContentType);

public sealed class ExportStockTransferPdfQueryHandler : IRequestHandler<ExportStockTransferPdfQuery, Result<StockTransferPdfResult>>
{
    private readonly IStockTransferRepository _stockTransferRepository;
    private readonly IPdfService _pdfService;
    private readonly IAuditService _auditService;

    public ExportStockTransferPdfQueryHandler(
        IStockTransferRepository stockTransferRepository,
        IPdfService pdfService,
        IAuditService auditService)
    {
        _stockTransferRepository = stockTransferRepository;
        _pdfService = pdfService;
        _auditService = auditService;
    }

    public async Task<Result<StockTransferPdfResult>> Handle(ExportStockTransferPdfQuery request, CancellationToken cancellationToken)
    {
        var transfer = await _stockTransferRepository.GetByIdWithLinesAsync(request.StockTransferId, cancellationToken);
        if (transfer is null)
            return Result.Failure<StockTransferPdfResult>(Error.NotFound("StockTransfer", request.StockTransferId));

        var pdfBytes = await _pdfService.GenerateStockTransferPdfAsync(transfer, cancellationToken);
        var fileName = $"Transfert_{transfer.Number.Value}.pdf";

        await _auditService.LogAsync(
            AuditActions.Export.Pdf,
            "StockTransfer",
            transfer.Id,
            cancellationToken: cancellationToken);

        return Result.Success(new StockTransferPdfResult(pdfBytes, fileName, "application/pdf"));
    }
}
