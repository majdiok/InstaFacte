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

namespace FactuTrust.Application.Features.PurchaseReceipts.Commands;

/// <summary>
/// Command to create a new purchase receipt (bon de réception).
/// </summary>
public sealed record CreatePurchaseReceiptCommand(CreatePurchaseReceiptDto Dto) : IRequest<Result<Guid>>;

/// <summary>
/// Handler for CreatePurchaseReceiptCommand.
/// </summary>
public sealed class CreatePurchaseReceiptCommandHandler : IRequestHandler<CreatePurchaseReceiptCommand, Result<Guid>>
{
    private readonly IPurchaseReceiptRepository _purchaseReceiptRepository;
    private readonly ISupplierRepository _supplierRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly IProductRepository _productRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;
    private readonly ITenantContext _tenantContext;
    private readonly IDocumentNumberService _documentNumberService;

    public CreatePurchaseReceiptCommandHandler(
        IPurchaseReceiptRepository purchaseReceiptRepository,
        ISupplierRepository supplierRepository,
        IWarehouseRepository warehouseRepository,
        IPurchaseOrderRepository purchaseOrderRepository,
        IProductRepository productRepository,
        ICurrentUser currentUser,
        IAuditService auditService,
        ITenantContext tenantContext,
        IDocumentNumberService documentNumberService)
    {
        _purchaseReceiptRepository = purchaseReceiptRepository;
        _supplierRepository = supplierRepository;
        _warehouseRepository = warehouseRepository;
        _purchaseOrderRepository = purchaseOrderRepository;
        _productRepository = productRepository;
        _currentUser = currentUser;
        _auditService = auditService;
        _tenantContext = tenantContext;
        _documentNumberService = documentNumberService;
    }

    public async Task<Result<Guid>> Handle(CreatePurchaseReceiptCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto;

        if (_tenantContext.TenantId is null)
            return Result.Failure<Guid>(Error.Unauthorized("Aucun contexte d'entreprise disponible."));

        var supplier = await _supplierRepository.GetByIdAsync(dto.SupplierId, cancellationToken);
        if (supplier is null)
            return Result.Failure<Guid>(Error.NotFound("Supplier", dto.SupplierId));

        if (!supplier.IsActive)
            return Result.Failure<Guid>(Error.Validation("Supplier", "Ce fournisseur est désactivé."));

        var warehouse = await _warehouseRepository.GetByIdAsync(dto.WarehouseId, cancellationToken);
        if (warehouse is null)
            return Result.Failure<Guid>(Error.NotFound("Warehouse", dto.WarehouseId));

        if (!warehouse.IsActive)
            return Result.Failure<Guid>(Error.Validation("Warehouse", "Cet entrepôt est désactivé."));

        if (dto.Lines.Count == 0)
            return Result.Failure<Guid>(Error.Validation("Lines", "Au moins une ligne est requise."));

        if (dto.PurchaseOrderId.HasValue)
        {
            var po = await _purchaseOrderRepository.GetByIdAsync(dto.PurchaseOrderId.Value, cancellationToken);
            if (po is null)
                return Result.Failure<Guid>(Error.NotFound("PurchaseOrder", dto.PurchaseOrderId.Value));

            if (po.SupplierId != dto.SupplierId)
                return Result.Failure<Guid>(Error.Validation("PurchaseOrderId",
                    "La commande d'achat n'appartient pas à ce fournisseur."));
        }

        var year = dto.ReceiptDate.Year;
        var docResult = await _documentNumberService.ReserveNextAsync(
            _tenantContext.TenantId!.Value,
            NumberingDocumentType.PurchaseReceipt,
            year,
            dto.ReceiptDate,
            cancellationToken);
        var number = PurchaseReceiptNumber.Create(docResult.Prefix ?? "BR", docResult.Year, docResult.Sequence);

        var receiptResult = PurchaseReceipt.Create(
            number,
            supplier,
            warehouse,
            dto.ReceiptDate,
            dto.PurchaseOrderId,
            dto.SupplierReference,
            dto.TransporterName,
            dto.DeliveryNoteNumber,
            dto.Notes);

        if (receiptResult.IsFailure)
            return Result.Failure<Guid>(receiptResult.Error);

        var receipt = receiptResult.Value;

        foreach (var lineDto in dto.Lines)
        {
            var product = await _productRepository.GetByIdAsync(lineDto.ProductId, cancellationToken);
            if (product is null)
                return Result.Failure<Guid>(Error.NotFound("Product", lineDto.ProductId));

            var unitPrice = lineDto.UnitPriceHT.HasValue
                ? Money.Create(lineDto.UnitPriceHT.Value, "TND")
                : product.GetPurchasePrice();

            var addLineResult = receipt.AddLine(
                product,
                lineDto.ReceivedQuantity,
                unitPrice,
                lineDto.OrderedQuantity,
                lineDto.PurchaseOrderLineId,
                lineDto.DiscountPercent);

            if (addLineResult.IsFailure)
                return Result.Failure<Guid>(addLineResult.Error);
        }

        receipt.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system");

        await _purchaseReceiptRepository.AddAsync(receipt, cancellationToken);

        await _auditService.LogAsync(
            AuditActions.PurchaseReceipt.Created,
            "PurchaseReceipt",
            receipt.Id,
            newValues: new { Number = receipt.Number.Value, SupplierName = supplier.Name },
            cancellationToken: cancellationToken);

        return Result.Success(receipt.Id);
    }
}
