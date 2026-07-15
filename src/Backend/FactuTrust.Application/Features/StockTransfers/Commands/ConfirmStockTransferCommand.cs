using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Application.Features.StockTransfers.Commands;

public sealed record ConfirmStockTransferCommand(Guid StockTransferId) : IRequest<Result>;

public sealed class ConfirmStockTransferCommandHandler : IRequestHandler<ConfirmStockTransferCommand, Result>
{
    private readonly IStockTransferRepository _repository;
    private readonly IStockItemRepository _stockItemRepository;
    private readonly IAuditService _auditService;
    private readonly ILogger<ConfirmStockTransferCommandHandler> _logger;

    public ConfirmStockTransferCommandHandler(
        IStockTransferRepository repository,
        IStockItemRepository stockItemRepository,
        IAuditService auditService,
        ILogger<ConfirmStockTransferCommandHandler> logger)
    {
        _repository = repository;
        _stockItemRepository = stockItemRepository;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<Result> Handle(ConfirmStockTransferCommand request, CancellationToken cancellationToken)
    {
        var transfer = await _repository.GetByIdWithLinesAsync(request.StockTransferId, cancellationToken);
        if (transfer is null)
            return Result.Failure(Error.NotFound("StockTransfer", request.StockTransferId));

        // Verify stock availability in source warehouse for each line
        foreach (var line in transfer.Lines)
        {
            var stockItem = await _stockItemRepository.GetByProductAndWarehouseAsync(
                line.ProductId, transfer.SourceWarehouseId, cancellationToken);

            if (stockItem is null || stockItem.QuantityAvailable < line.RequestedQuantity)
            {
                var available = stockItem?.QuantityAvailable ?? 0;
                return Result.Failure(Error.Validation("Stock",
                    $"Stock insuffisant pour le produit '{line.ProductName}' dans l'entrepôt source. " +
                    $"Disponible: {available}, Demandé: {line.RequestedQuantity}"));
            }
        }

        var result = transfer.Confirm();
        if (result.IsFailure)
            return result;

        await _repository.UpdateAsync(transfer, cancellationToken);

        try
        {
            await _auditService.LogAsync(
                "StockTransfer.Confirmed",
                "StockTransfer",
                transfer.Id,
                newValues: new { Number = transfer.Number.Value },
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audit logging failed for StockTransfer.Confirmed ({StockTransferId})", request.StockTransferId);
        }

        return Result.Success();
    }
}
