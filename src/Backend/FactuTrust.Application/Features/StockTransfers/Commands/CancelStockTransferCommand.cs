using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Application.Features.StockTransfers.Commands;

public sealed record CancelStockTransferCommand(Guid StockTransferId, string Reason) : IRequest<Result>;

public sealed class CancelStockTransferCommandHandler : IRequestHandler<CancelStockTransferCommand, Result>
{
    private readonly IStockTransferRepository _repository;
    private readonly IAuditService _auditService;
    private readonly ILogger<CancelStockTransferCommandHandler> _logger;

    public CancelStockTransferCommandHandler(
        IStockTransferRepository repository,
        IAuditService auditService,
        ILogger<CancelStockTransferCommandHandler> logger)
    {
        _repository = repository;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<Result> Handle(CancelStockTransferCommand request, CancellationToken cancellationToken)
    {
        var transfer = await _repository.GetByIdWithLinesAsync(request.StockTransferId, cancellationToken);
        if (transfer is null)
            return Result.Failure(Error.NotFound("StockTransfer", request.StockTransferId));

        var result = transfer.Cancel(request.Reason);
        if (result.IsFailure)
            return result;

        await _repository.UpdateAsync(transfer, cancellationToken);

        try
        {
            await _auditService.LogAsync(
                "StockTransfer.Cancelled",
                "StockTransfer",
                transfer.Id,
                newValues: new { Number = transfer.Number.Value, Reason = request.Reason },
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audit logging failed for StockTransfer.Cancelled ({StockTransferId})", request.StockTransferId);
        }

        return Result.Success();
    }
}
