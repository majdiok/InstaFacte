using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using AuditActions = FactuTrust.Domain.Entities.AuditActions;
using MediatR;

namespace FactuTrust.Application.Features.PurchaseOrders.Commands;

/// <summary>
/// Command to confirm a purchase order (Draft → Confirmed).
/// </summary>
public sealed record ConfirmPurchaseOrderCommand(Guid Id) : IRequest<Result>;

/// <summary>
/// Handler for ConfirmPurchaseOrderCommand.
/// </summary>
public sealed class ConfirmPurchaseOrderCommandHandler : IRequestHandler<ConfirmPurchaseOrderCommand, Result>
{
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly IAuditService _auditService;

    public ConfirmPurchaseOrderCommandHandler(
        IPurchaseOrderRepository purchaseOrderRepository,
        IAuditService auditService)
    {
        _purchaseOrderRepository = purchaseOrderRepository;
        _auditService = auditService;
    }

    public async Task<Result> Handle(ConfirmPurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        var po = await _purchaseOrderRepository.GetByIdWithLinesAsync(request.Id, cancellationToken);
        if (po is null)
            return Result.Failure(Error.NotFound("PurchaseOrder", request.Id));

        var result = po.Confirm();
        if (result.IsFailure)
            return result;

        var persisted = await _purchaseOrderRepository.TryConfirmDraftAsync(
            po.Id,
            po.ConfirmedAt ?? DateTime.UtcNow,
            cancellationToken);
        if (!persisted)
        {
            return Result.Failure(Error.Conflict(
                "Cette commande a été modifiée entre-temps. Actualisez la page avant de réessayer."));
        }

        await _auditService.LogAsync(
            AuditActions.PurchaseOrder.Confirmed,
            "PurchaseOrder",
            po.Id,
            newValues: new { Number = po.Number.Value, Status = po.Status.ToString() },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}
