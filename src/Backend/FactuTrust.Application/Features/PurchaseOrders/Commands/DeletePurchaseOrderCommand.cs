using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using AuditActions = FactuTrust.Domain.Entities.AuditActions;
using MediatR;

namespace FactuTrust.Application.Features.PurchaseOrders.Commands;

/// <summary>
/// Command to delete a draft purchase order.
/// Only draft orders can be deleted.
/// </summary>
public sealed record DeletePurchaseOrderCommand(Guid Id) : IRequest<Result>;

/// <summary>
/// Handler for DeletePurchaseOrderCommand.
/// </summary>
public sealed class DeletePurchaseOrderCommandHandler : IRequestHandler<DeletePurchaseOrderCommand, Result>
{
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;

    public DeletePurchaseOrderCommandHandler(
        IPurchaseOrderRepository purchaseOrderRepository,
        ICurrentUser currentUser,
        IAuditService auditService)
    {
        _purchaseOrderRepository = purchaseOrderRepository;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<Result> Handle(DeletePurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        var po = await _purchaseOrderRepository.GetByIdAsync(request.Id, cancellationToken);
        if (po is null)
            return Result.Failure(Error.NotFound("PurchaseOrder", request.Id));

        if (!po.Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Seuls les brouillons peuvent être supprimés."));

        var number = po.Number.Value;

        await _purchaseOrderRepository.DeleteAsync(po, cancellationToken);

        await _auditService.LogAsync(
            AuditActions.PurchaseOrder.Deleted,
            "PurchaseOrder",
            po.Id,
            newValues: new { Number = number },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}
