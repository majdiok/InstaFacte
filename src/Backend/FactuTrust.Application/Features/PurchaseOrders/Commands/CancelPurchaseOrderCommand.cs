using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using AuditActions = FactuTrust.Domain.Entities.AuditActions;
using MediatR;

namespace FactuTrust.Application.Features.PurchaseOrders.Commands;

/// <summary>
/// Command to cancel a purchase order.
/// </summary>
public sealed record CancelPurchaseOrderCommand(Guid Id, string Reason) : IRequest<Result>;

/// <summary>
/// Handler for CancelPurchaseOrderCommand.
/// </summary>
public sealed class CancelPurchaseOrderCommandHandler : IRequestHandler<CancelPurchaseOrderCommand, Result>
{
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly IAuditService _auditService;

    public CancelPurchaseOrderCommandHandler(
        IPurchaseOrderRepository purchaseOrderRepository,
        IAuditService auditService)
    {
        _purchaseOrderRepository = purchaseOrderRepository;
        _auditService = auditService;
    }

    public async Task<Result> Handle(CancelPurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        var po = await _purchaseOrderRepository.GetByIdAsync(request.Id, cancellationToken);
        if (po is null)
            return Result.Failure(Error.NotFound("PurchaseOrder", request.Id));

        var result = po.Cancel(request.Reason);
        if (result.IsFailure)
            return result;

        await _purchaseOrderRepository.UpdateAsync(po, cancellationToken);

        await _auditService.LogAsync(
            AuditActions.PurchaseOrder.Cancelled,
            "PurchaseOrder",
            po.Id,
            newValues: new { Number = po.Number.Value, Reason = request.Reason },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}
