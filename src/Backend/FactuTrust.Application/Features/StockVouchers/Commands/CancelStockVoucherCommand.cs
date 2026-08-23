using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using AuditActions = FactuTrust.Domain.Entities.AuditActions;
using MediatR;

namespace FactuTrust.Application.Features.StockVouchers.Commands;

public sealed record CancelStockVoucherCommand(Guid Id, string Reason) : IRequest<Result>;

public sealed class CancelStockVoucherCommandHandler : IRequestHandler<CancelStockVoucherCommand, Result>
{
    private readonly IStockVoucherMovementService _movementService;
    private readonly IAuditService _auditService;

    public CancelStockVoucherCommandHandler(
        IStockVoucherMovementService movementService,
        IAuditService auditService)
    {
        _movementService = movementService;
        _auditService = auditService;
    }

    public async Task<Result> Handle(CancelStockVoucherCommand request, CancellationToken cancellationToken)
    {
        var result = await _movementService.CancelAsync(request.Id, request.Reason, cancellationToken);
        if (result.IsFailure)
            return result;

        await _auditService.LogAsync(
            AuditActions.StockVoucher.Cancelled,
            "StockVoucher",
            request.Id,
            newValues: new { request.Reason },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}
