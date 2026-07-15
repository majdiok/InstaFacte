using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services;
using FactuTrust.Domain.ValueObjects;
using AuditActions = FactuTrust.Domain.Entities.AuditActions;
using MediatR;

namespace FactuTrust.Application.Features.PurchaseOrders.Commands;

/// <summary>
/// Command to create a new purchase order.
/// </summary>
public sealed record CreatePurchaseOrderCommand(CreatePurchaseOrderDto Dto) : IRequest<Result<Guid>>;

/// <summary>
/// Handler for CreatePurchaseOrderCommand.
/// </summary>
public sealed class CreatePurchaseOrderCommandHandler : IRequestHandler<CreatePurchaseOrderCommand, Result<Guid>>
{
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly ISupplierRepository _supplierRepository;
    private readonly IProductRepository _productRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;
    private readonly ITenantContext _tenantContext;
    private readonly IDocumentNumberService _documentNumberService;

    public CreatePurchaseOrderCommandHandler(
        IPurchaseOrderRepository purchaseOrderRepository,
        ISupplierRepository supplierRepository,
        IProductRepository productRepository,
        IWarehouseRepository warehouseRepository,
        ICurrentUser currentUser,
        IAuditService auditService,
        ITenantContext tenantContext,
        IDocumentNumberService documentNumberService)
    {
        _purchaseOrderRepository = purchaseOrderRepository;
        _supplierRepository = supplierRepository;
        _productRepository = productRepository;
        _warehouseRepository = warehouseRepository;
        _currentUser = currentUser;
        _auditService = auditService;
        _tenantContext = tenantContext;
        _documentNumberService = documentNumberService;
    }

    public async Task<Result<Guid>> Handle(CreatePurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto;

        if (_tenantContext.TenantId is null)
            return Result.Failure<Guid>(Error.Unauthorized("Aucun contexte d'entreprise disponible."));

        // Validate supplier
        var supplier = await _supplierRepository.GetByIdAsync(dto.SupplierId, cancellationToken);
        if (supplier is null)
            return Result.Failure<Guid>(Error.NotFound("Supplier", dto.SupplierId));

        if (!supplier.IsActive)
            return Result.Failure<Guid>(Error.Validation("Supplier", "Ce fournisseur est désactivé."));

        if (dto.Lines.Count == 0)
            return Result.Failure<Guid>(Error.Validation("Lines", "Au moins une ligne est requise."));

        // Validate warehouse if provided
        if (dto.WarehouseId.HasValue)
        {
            var warehouse = await _warehouseRepository.GetByIdAsync(dto.WarehouseId.Value, cancellationToken);
            if (warehouse is null)
                return Result.Failure<Guid>(Error.NotFound("Warehouse", dto.WarehouseId.Value));
            if (!warehouse.IsActive)
                return Result.Failure<Guid>(Error.Validation("Warehouse", "Cet entrepôt est désactivé."));
        }

        // Generate next number (atomic sequence)
        var year = dto.OrderDate.Year;
        var docResult = await _documentNumberService.ReserveNextAsync(
            _tenantContext.TenantId!.Value,
            NumberingDocumentType.PurchaseOrder,
            year,
            dto.OrderDate,
            cancellationToken);
        var number = PurchaseOrderNumber.Create(docResult.Prefix ?? "BC", docResult.Year, docResult.Sequence);

        // Create the purchase order
        var poResult = PurchaseOrder.Create(
            number,
            supplier,
            dto.OrderDate,
            dto.ExpectedDeliveryDate,
            dto.Reference?.Trim(),
            dto.Notes?.Trim(),
            dto.WarehouseId);

        if (poResult.IsFailure)
            return Result.Failure<Guid>(poResult.Error);

        var purchaseOrder = poResult.Value;

        // Add lines
        foreach (var lineDto in dto.Lines)
        {
            var product = await _productRepository.GetByIdAsync(lineDto.ProductId, cancellationToken);
            if (product is null)
                return Result.Failure<Guid>(Error.NotFound("Product", lineDto.ProductId));

            var unitPrice = lineDto.UnitPriceHT.HasValue
                ? Money.Create(lineDto.UnitPriceHT.Value, "TND")
                : product.GetPurchasePrice();

            var addLineResult = purchaseOrder.AddLine(product, lineDto.Quantity, unitPrice);
            if (addLineResult.IsFailure)
                return Result.Failure<Guid>(addLineResult.Error);
        }

        purchaseOrder.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system");

        await _purchaseOrderRepository.AddAsync(purchaseOrder, cancellationToken);

        await _auditService.LogAsync(
            AuditActions.PurchaseOrder.Created,
            "PurchaseOrder",
            purchaseOrder.Id,
            newValues: new { Number = purchaseOrder.Number.Value, SupplierName = supplier.Name },
            cancellationToken: cancellationToken);

        return Result.Success(purchaseOrder.Id);
    }
}
