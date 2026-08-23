using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using AuditActions = FactuTrust.Domain.Entities.AuditActions;
using MediatR;

namespace FactuTrust.Application.Features.StockVouchers.Queries;

public sealed record ExportStockVoucherPdfQuery(Guid Id) : IRequest<Result<StockVoucherPdfResult>>;

public sealed record StockVoucherPdfResult(byte[] Content, string FileName, string ContentType);

public sealed class ExportStockVoucherPdfQueryHandler
    : IRequestHandler<ExportStockVoucherPdfQuery, Result<StockVoucherPdfResult>>
{
    private readonly IStockVoucherRepository _repository;
    private readonly IPdfService _pdfService;
    private readonly IAuditService _auditService;

    public ExportStockVoucherPdfQueryHandler(
        IStockVoucherRepository repository,
        IPdfService pdfService,
        IAuditService auditService)
    {
        _repository = repository;
        _pdfService = pdfService;
        _auditService = auditService;
    }

    public async Task<Result<StockVoucherPdfResult>> Handle(
        ExportStockVoucherPdfQuery request,
        CancellationToken cancellationToken)
    {
        var voucher = await _repository.GetByIdWithLinesAsync(request.Id, cancellationToken);
        if (voucher is null)
            return Result.Failure<StockVoucherPdfResult>(Error.NotFound("StockVoucher", request.Id));

        var pdfBytes = await _pdfService.GenerateStockVoucherPdfAsync(voucher, cancellationToken);
        var fileName = $"{voucher.Kind.DefaultPrefix()}_{voucher.Number.Value}.pdf";

        await _auditService.LogAsync(
            AuditActions.StockVoucher.Exported,
            "StockVoucher",
            voucher.Id,
            newValues: new { Number = voucher.Number.Value },
            cancellationToken: cancellationToken);

        return Result.Success(new StockVoucherPdfResult(pdfBytes, fileName, "application/pdf"));
    }
}
