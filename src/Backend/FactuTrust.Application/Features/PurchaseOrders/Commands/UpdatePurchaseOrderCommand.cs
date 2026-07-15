using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using AuditActions = FactuTrust.Domain.Entities.AuditActions;
using MediatR;

namespace FactuTrust.Application.Features.PurchaseOrders.Commands;

/// <summary>
/// Command to update a draft purchase order.
/// Only draft orders can be modified.
/// </summary>
public sealed record UpdatePurchaseOrderCommand(Guid Id, UpdatePurchaseOrderDto Dto) : IRequest<Result>;

/// <summary>
/// Handler for UpdatePurchaseOrderCommand.
/// </summary>
public sealed class UpdatePurchaseOrderCommandHandler : IRequestHandler<UpdatePurchaseOrderCommand, Result>
{
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly IProductRepository _productRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;

    public UpdatePurchaseOrderCommandHandler(
        IPurchaseOrderRepository purchaseOrderRepository,
        IProductRepository productRepository,
        ICurrentUser currentUser,
        IAuditService auditService)
    {
        _purchaseOrderRepository = purchaseOrderRepository;
        _productRepository = productRepository;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<Result> Handle(UpdatePurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        var po = await _purchaseOrderRepository.GetByIdWithLinesAsync(request.Id, cancellationToken);
        if (po is null)
            return Result.Failure(Error.NotFound("PurchaseOrder", request.Id));

        if (!po.Status.CanBeEdited())
            return Result.Failure(Error.Validation("Status", "Seuls les brouillons peuvent être modifiés."));

        var dto = request.Dto;

        // Update header fields
        if (dto.ExpectedDeliveryDate.HasValue && dto.ExpectedDeliveryDate.Value < po.OrderDate)
            return Result.Failure(Error.Validation("ExpectedDeliveryDate",
                "La date de livraison prévue doit être postérieure à la date de commande."));

        po.UpdateHeader(dto.ExpectedDeliveryDate, dto.Reference, dto.Notes);

        // Remove lines that are no longer in the DTO
        if (dto.Lines is not null)
        {
            var dtoLineIds = dto.Lines.Where(l => l.Id.HasValue).Select(l => l.Id!.Value).ToHashSet();
            var linesToRemove = po.Lines.Where(l => !dtoLineIds.Contains(l.Id)).Select(l => l.Id).ToList();

            foreach (var lineId in linesToRemove)
            {
                var removeResult = po.RemoveLine(lineId);
                if (removeResult.IsFailure) return removeResult;
            }

            // Update existing lines and add new ones
            foreach (var lineDto in dto.Lines)
            {
                if (lineDto.Id.HasValue)
                {
                    // Update existing line
                    var unitPrice = lineDto.UnitPriceHT.HasValue
                        ? Money.Create(lineDto.UnitPriceHT.Value, "TND")
                        : (Money?)null;

                    var updateResult = po.UpdateLine(lineDto.Id.Value, lineDto.Quantity, unitPrice);
                    if (updateResult.IsFailure) return updateResult;
                }
                else
                {
                    // Add new line
                    var product = await _productRepository.GetByIdAsync(lineDto.ProductId, cancellationToken);
                    if (product is null)
                        return Result.Failure(Error.NotFound("Product", lineDto.ProductId));

                    var unitPrice = lineDto.UnitPriceHT.HasValue
                        ? Money.Create(lineDto.UnitPriceHT.Value, "TND")
                        : product.GetPurchasePrice();

                    var addResult = po.AddLine(product, lineDto.Quantity, unitPrice);
                    if (addResult.IsFailure) return addResult;
                }
            }
        }

        po.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system");

        await _purchaseOrderRepository.UpdateAsync(po, cancellationToken);

        await _auditService.LogAsync(
            AuditActions.PurchaseOrder.Updated,
            "PurchaseOrder",
            po.Id,
            newValues: new { Number = po.Number.Value },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}
