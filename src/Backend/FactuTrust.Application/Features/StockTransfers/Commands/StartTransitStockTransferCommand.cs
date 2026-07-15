using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Application.Features.StockTransfers.Commands;

public sealed record StartTransitStockTransferCommand(Guid StockTransferId) : IRequest<Result>;

public sealed class StartTransitStockTransferCommandHandler : IRequestHandler<StartTransitStockTransferCommand, Result>
{
    private readonly IStockTransferRepository _repository;
    private readonly IAuditService _auditService;
    private readonly ILogger<StartTransitStockTransferCommandHandler> _logger;

    public StartTransitStockTransferCommandHandler(
        IStockTransferRepository repository,
        IAuditService auditService,
        ILogger<StartTransitStockTransferCommandHandler> logger)
    {
        _repository = repository;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<Result> Handle(StartTransitStockTransferCommand request, CancellationToken cancellationToken)
    {
        var transfer = await _repository.GetByIdWithLinesAsync(request.StockTransferId, cancellationToken);
        if (transfer is null)
            return Result.Failure(Error.NotFound("StockTransfer", request.StockTransferId));

        var startResult = transfer.StartTransit();
        if (startResult.IsFailure)
            return startResult;

        await _repository.UpdateAsync(transfer, cancellationToken);

        try
        {
            await _auditService.LogAsync(
                "StockTransfer.StartTransit",
                "StockTransfer",
                transfer.Id,
                newValues: new { Number = transfer.Number.Value },
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audit logging failed for StockTransfer.StartTransit ({StockTransferId})", request.StockTransferId);
        }

        return Result.Success();
    }
}
