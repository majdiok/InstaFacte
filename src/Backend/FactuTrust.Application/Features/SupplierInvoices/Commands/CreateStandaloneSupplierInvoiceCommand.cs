using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.SupplierInvoices.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;
using AppCommon = FactuTrust.Application.Common;

namespace FactuTrust.Application.Features.SupplierInvoices.Commands;

public sealed record CreateStandaloneSupplierInvoiceCommand(CreateStandaloneSupplierInvoiceDto Dto)
    : IRequest<Result<SupplierInvoiceCreationResult>>;

public sealed class CreateStandaloneSupplierInvoiceCommandHandler
    : IRequestHandler<CreateStandaloneSupplierInvoiceCommand, Result<SupplierInvoiceCreationResult>>
{
    private readonly ISupplierRepository _supplierRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IProductRepository _productRepository;
    private readonly ISupplierInvoiceRepository _supplierInvoiceRepository;
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly IAuditService _auditService;
    private readonly ILogger<CreateStandaloneSupplierInvoiceCommandHandler> _logger;
    private readonly IPublisher _publisher;
    private readonly IWithholdingTaxRepository _withholdingTaxRepository;
    private readonly IWithholdingTaxService _withholdingTaxService;
    private readonly IWithholdingFiscalYearParameterRepository _fiscalYearParameters;
    private readonly ISupplierInvoiceNumberService _supplierInvoiceNumberService;

    public CreateStandaloneSupplierInvoiceCommandHandler(
        ISupplierRepository supplierRepository,
        IWarehouseRepository warehouseRepository,
        IProductRepository productRepository,
        ISupplierInvoiceRepository supplierInvoiceRepository,
        IPurchaseOrderRepository purchaseOrderRepository,
        IAuditService auditService,
        ILogger<CreateStandaloneSupplierInvoiceCommandHandler> logger,
        IPublisher publisher,
        IWithholdingTaxRepository withholdingTaxRepository,
        IWithholdingTaxService withholdingTaxService,
        IWithholdingFiscalYearParameterRepository fiscalYearParameters,
        ISupplierInvoiceNumberService supplierInvoiceNumberService)
    {
        _supplierRepository = supplierRepository;
        _warehouseRepository = warehouseRepository;
        _productRepository = productRepository;
        _supplierInvoiceRepository = supplierInvoiceRepository;
        _purchaseOrderRepository = purchaseOrderRepository;
        _auditService = auditService;
        _logger = logger;
        _publisher = publisher;
        _withholdingTaxRepository = withholdingTaxRepository;
        _withholdingTaxService = withholdingTaxService;
        _fiscalYearParameters = fiscalYearParameters;
        _supplierInvoiceNumberService = supplierInvoiceNumberService;
    }

    public async Task<Result<SupplierInvoiceCreationResult>> Handle(
        CreateStandaloneSupplierInvoiceCommand request,
        CancellationToken cancellationToken)
    {
        var dto = request.Dto;

        var supplier = await _supplierRepository.GetByIdAsync(dto.SupplierId, cancellationToken);
        if (supplier is null)
            return Result.Failure<SupplierInvoiceCreationResult>(Error.NotFound("Supplier", dto.SupplierId));

        if (!supplier.IsActive)
            return Result.Failure<SupplierInvoiceCreationResult>(Error.Validation("Supplier",
                "Ce fournisseur est désactivé."));

        Guid? warehouseId = null;
        if (dto.WarehouseId is { } requestedWarehouseId)
        {
            var warehouse = await _warehouseRepository.GetByIdAsync(requestedWarehouseId, cancellationToken);
            if (warehouse is null)
                return Result.Failure<SupplierInvoiceCreationResult>(Error.NotFound("Warehouse", requestedWarehouseId));

            if (!warehouse.IsActive)
                return Result.Failure<SupplierInvoiceCreationResult>(Error.Validation("Warehouse",
                    "Cet entrepôt est désactivé."));

            warehouseId = warehouse.Id;
        }

        if (dto.Lines.Count == 0)
            return Result.Failure<SupplierInvoiceCreationResult>(Error.Validation("Lines",
                "Sélectionnez au moins une ligne à facturer"));

        var trimmedExternal = string.IsNullOrWhiteSpace(dto.ExternalReference)
            ? null
            : dto.ExternalReference.Trim();

        if (!string.IsNullOrEmpty(trimmedExternal))
        {
            var duplicate = await HasDuplicateExternalReferenceAsync(
                dto.SupplierId, trimmedExternal, cancellationToken);
            if (duplicate)
                return Result.Failure<SupplierInvoiceCreationResult>(Error.Conflict(
                    $"La référence fournisseur '{trimmedExternal}' existe déjà pour ce fournisseur."));
        }

        var lineInputs = new List<StandaloneSupplierInvoiceLineInput>(dto.Lines.Count);
        var lineNumber = 1;
        var assetClassifications = new List<(int LineNumber, bool IsFixedAsset, string? AssetAccountNumber, Guid? DepreciationRateCategoryId)>();

        foreach (var lineDto in dto.Lines)
        {
            var product = await _productRepository.GetByIdAsync(lineDto.ProductId, cancellationToken);
            if (product is null)
                return Result.Failure<SupplierInvoiceCreationResult>(Error.NotFound("Product", lineDto.ProductId));

            if (!product.IsActive)
                return Result.Failure<SupplierInvoiceCreationResult>(Error.Validation("Product",
                    $"Le produit '{product.Code}' est désactivé."));

            if (product.IsStockManaged)
                return Result.Failure<SupplierInvoiceCreationResult>(Error.Validation("Product",
                    $"L'article '{product.Code}' est géré en stock. Utilisez un bon de réception, puis créez la facture depuis le BC ou le BR."));

            var unitPrice = lineDto.UnitPriceHt.HasValue
                ? Money.Create(lineDto.UnitPriceHt.Value)
                : product.GetPurchasePrice();

            lineInputs.Add(new StandaloneSupplierInvoiceLineInput(
                product.Id,
                product.Code,
                product.Name,
                product.Description,
                lineDto.Quantity,
                product.Unit,
                unitPrice,
                product.VatRate,
                lineDto.DiscountPercent ?? 0m));

            if (lineDto.IsFixedAsset)
            {
                assetClassifications.Add((
                    lineNumber,
                    true,
                    lineDto.AssetAccountNumber,
                    lineDto.DepreciationRateCategoryId));
            }

            lineNumber++;
        }

        var invoiceNumberResult = await SupplierInvoiceNumberResolver.ResolveAsync(
            dto.InvoiceNumber,
            dto.UseSuggestedNumber,
            dto.InvoiceDate,
            _supplierInvoiceNumberService,
            cancellationToken);
        if (invoiceNumberResult.IsFailure)
            return Result.Failure<SupplierInvoiceCreationResult>(invoiceNumberResult.Error);

        var result = SupplierInvoice.CreateStandalone(
            supplier,
            invoiceNumberResult.Value,
            dto.InvoiceDate,
            lineInputs,
            dto.PaymentTermDays <= 0 ? 30 : dto.PaymentTermDays,
            trimmedExternal,
            dto.Notes,
            dto.PaymentMethod,
            warehouseId);

        if (result.IsFailure)
            return Result.Failure<SupplierInvoiceCreationResult>(result.Error);

        var invoice = result.Value;
        if (assetClassifications.Count > 0)
            invoice.ApplyLineAssetClassifications(assetClassifications);

        var numberWasAutoResolved = dto.UseSuggestedNumber
            || string.IsNullOrWhiteSpace(dto.InvoiceNumber);

        return await SupplierInvoiceCreationHelper.PersistAndFinalizeAsync(
            invoice,
            purchaseOrder: null,
            purchaseReceipt: null,
            sendEmail: false,
            _auditService,
            _supplierInvoiceRepository,
            _purchaseOrderRepository,
            purchaseReceiptRepository: null,
            _withholdingTaxRepository,
            _withholdingTaxService,
            _fiscalYearParameters,
            _publisher,
            _logger,
            _supplierInvoiceNumberService,
            numberWasAutoResolved,
            cancellationToken);
    }

    private async Task<bool> HasDuplicateExternalReferenceAsync(
        Guid supplierId,
        string externalReference,
        CancellationToken cancellationToken)
    {
        var key = AppCommon.SupplierInvoiceNumberNormalizer.Normalize(externalReference);
        if (key.Length == 0)
            return false;

        var existing = await _supplierInvoiceRepository
            .GetNonCancelledExternalReferencesForSupplierAsync(supplierId, cancellationToken);

        return existing.Any(r =>
            AppCommon.SupplierInvoiceNumberNormalizer.Normalize(r) == key);
    }
}
