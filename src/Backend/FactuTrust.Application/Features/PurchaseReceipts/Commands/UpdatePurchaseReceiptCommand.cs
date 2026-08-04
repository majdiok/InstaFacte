using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using AuditActions = FactuTrust.Domain.Entities.AuditActions;
using MediatR;

namespace FactuTrust.Application.Features.PurchaseReceipts.Commands;

/// <summary>
/// Command to update a draft purchase receipt.
/// Only draft receipts can be modified; lines are cleared and re-added.
/// </summary>
public sealed record UpdatePurchaseReceiptCommand(Guid Id, UpdatePurchaseReceiptDto Dto) : IRequest<Result>;

/// <summary>
/// Handler for UpdatePurchaseReceiptCommand.
/// </summary>
public sealed class UpdatePurchaseReceiptCommandHandler : IRequestHandler<UpdatePurchaseReceiptCommand, Result>
{
    private readonly IPurchaseReceiptRepository _purchaseReceiptRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IProductRepository _productRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;

    public UpdatePurchaseReceiptCommandHandler(
        IPurchaseReceiptRepository purchaseReceiptRepository,
        IWarehouseRepository warehouseRepository,
        IProductRepository productRepository,
        ICurrentUser currentUser,
        IAuditService auditService)
    {
        _purchaseReceiptRepository = purchaseReceiptRepository;
        _warehouseRepository = warehouseRepository;
        _productRepository = productRepository;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<Result> Handle(UpdatePurchaseReceiptCommand request, CancellationToken cancellationToken)
    {
        var receipt = await _purchaseReceiptRepository.GetByIdWithLinesAsync(request.Id, cancellationToken);
        if (receipt is null)
            return Result.Failure(Error.NotFound("PurchaseReceipt", request.Id));

        if (!receipt.Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Seuls les brouillons peuvent être modifiés."));

        var dto = request.Dto;

        var warehouse = await _warehouseRepository.GetByIdAsync(dto.WarehouseId, cancellationToken);
        if (warehouse is null)
            return Result.Failure(Error.NotFound("Warehouse", dto.WarehouseId));

        if (!warehouse.IsActive)
            return Result.Failure(Error.Validation("Warehouse", "Cet entrepôt est désactivé."));

        if (dto.Lines.Count == 0)
            return Result.Failure(Error.Validation("Lines", "Au moins une ligne est requise."));

        receipt.UpdateHeader(
            dto.ReceiptDate,
            dto.WarehouseId,
            dto.SupplierReference,
            dto.TransporterName,
            dto.DeliveryNoteNumber,
            dto.Notes);

        var clearResult = receipt.ClearLines();
        if (clearResult.IsFailure)
            return clearResult;

        foreach (var lineDto in dto.Lines)
        {
            var product = await _productRepository.GetByIdAsync(lineDto.ProductId, cancellationToken);
            if (product is null)
                return Result.Failure(Error.NotFound("Product", lineDto.ProductId));

            var unitPrice = lineDto.UnitPriceHT.HasValue
                ? Money.Create(lineDto.UnitPriceHT.Value, "TND")
                : product.GetPurchasePrice();

            var addResult = receipt.AddLine(
                product,
                lineDto.ReceivedQuantity,
                unitPrice,
                lineDto.OrderedQuantity,
                lineDto.PurchaseOrderLineId,
                lineDto.DiscountPercent);

            if (addResult.IsFailure)
                return addResult;
        }

        receipt.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system");

        await _purchaseReceiptRepository.UpdateAsync(receipt, cancellationToken);

        await _auditService.LogAsync(
            AuditActions.PurchaseReceipt.Updated,
            "PurchaseReceipt",
            receipt.Id,
            newValues: new { Number = receipt.Number.Value },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}
