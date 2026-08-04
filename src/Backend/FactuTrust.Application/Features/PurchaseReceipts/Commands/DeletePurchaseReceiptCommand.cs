using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using AuditActions = FactuTrust.Domain.Entities.AuditActions;
using MediatR;

namespace FactuTrust.Application.Features.PurchaseReceipts.Commands;

/// <summary>
/// Command to delete a draft purchase receipt.
/// Only draft receipts can be deleted.
/// </summary>
public sealed record DeletePurchaseReceiptCommand(Guid Id) : IRequest<Result>;

/// <summary>
/// Handler for DeletePurchaseReceiptCommand.
/// </summary>
public sealed class DeletePurchaseReceiptCommandHandler : IRequestHandler<DeletePurchaseReceiptCommand, Result>
{
    private readonly IPurchaseReceiptRepository _purchaseReceiptRepository;
    private readonly IAuditService _auditService;

    public DeletePurchaseReceiptCommandHandler(
        IPurchaseReceiptRepository purchaseReceiptRepository,
        IAuditService auditService)
    {
        _purchaseReceiptRepository = purchaseReceiptRepository;
        _auditService = auditService;
    }

    public async Task<Result> Handle(DeletePurchaseReceiptCommand request, CancellationToken cancellationToken)
    {
        var receipt = await _purchaseReceiptRepository.GetByIdAsync(request.Id, cancellationToken);
        if (receipt is null)
            return Result.Failure(Error.NotFound("PurchaseReceipt", request.Id));

        if (!receipt.Status.CanBeDeleted())
            return Result.Failure(Error.Validation("Status", "Seuls les brouillons peuvent être supprimés."));

        var number = receipt.Number.Value;

        await _purchaseReceiptRepository.DeleteAsync(receipt, cancellationToken);

        await _auditService.LogAsync(
            AuditActions.PurchaseReceipt.Deleted,
            "PurchaseReceipt",
            receipt.Id,
            newValues: new { Number = number },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}
