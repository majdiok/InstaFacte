using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Application.Features.StockTransfers.Commands;

public sealed record CompleteStockTransferCommand(Guid StockTransferId) : IRequest<Result>;

public sealed class CompleteStockTransferCommandHandler : IRequestHandler<CompleteStockTransferCommand, Result>
{
    private readonly IStockTransferCompletionService _completionService;
    private readonly IAuditService _auditService;
    private readonly ILogger<CompleteStockTransferCommandHandler> _logger;

    public CompleteStockTransferCommandHandler(
        IStockTransferCompletionService completionService,
        IAuditService auditService,
        ILogger<CompleteStockTransferCommandHandler> logger)
    {
        _completionService = completionService;
        _auditService = auditService;
        _logger = logger;
    }

    public async Task<Result> Handle(CompleteStockTransferCommand request, CancellationToken cancellationToken)
    {
        var result = await _completionService.CompleteTransferAsync(request.StockTransferId, cancellationToken);
        if (result.IsFailure)
            return result;

        try
        {
            await _auditService.LogAsync(
                "StockTransfer.Completed",
                "StockTransfer",
                request.StockTransferId,
                newValues: new { StockTransferId = request.StockTransferId },
                cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audit logging failed for StockTransfer.Completed ({StockTransferId})", request.StockTransferId);
        }

        return Result.Success();
    }
}
