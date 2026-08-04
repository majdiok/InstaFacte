using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Features.SupplierInvoices.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Application.Features.PurchaseReceipts.Commands;

public sealed record CreateSupplierInvoiceFromPurchaseReceiptCommand(
    Guid PurchaseReceiptId,
    string InvoiceNumber,
    DateTime InvoiceDate,
    int PaymentTermDays = 30,
    string? ExternalReference = null,
    string? Notes = null,
    bool SendEmail = false,
    IReadOnlyList<CreateSupplierInvoiceLineSelection>? Lines = null,
    IReadOnlyList<SupplierInvoiceLineAssetRequest>? LineAssetClassifications = null,
    string? PaymentMethod = null,
    bool UseSuggestedNumber = false
) : IRequest<Result<SupplierInvoiceCreationResult>>;

public sealed class CreateSupplierInvoiceFromPurchaseReceiptCommandHandler
    : IRequestHandler<CreateSupplierInvoiceFromPurchaseReceiptCommand, Result<SupplierInvoiceCreationResult>>
{
    private readonly IPurchaseReceiptRepository _purchaseReceiptRepository;
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly ISupplierInvoiceRepository _supplierInvoiceRepository;
    private readonly IAuditService _auditService;
    private readonly ILogger<CreateSupplierInvoiceFromPurchaseReceiptCommandHandler> _logger;
    private readonly IPublisher _publisher;
    private readonly IWithholdingTaxRepository _withholdingTaxRepository;
    private readonly IWithholdingTaxService _withholdingTaxService;
    private readonly IWithholdingFiscalYearParameterRepository _fiscalYearParameters;
    private readonly ISupplierInvoiceNumberService _supplierInvoiceNumberService;

    public CreateSupplierInvoiceFromPurchaseReceiptCommandHandler(
        IPurchaseReceiptRepository purchaseReceiptRepository,
        IPurchaseOrderRepository purchaseOrderRepository,
        ISupplierInvoiceRepository supplierInvoiceRepository,
        IAuditService auditService,
        ILogger<CreateSupplierInvoiceFromPurchaseReceiptCommandHandler> logger,
        IPublisher publisher,
        IWithholdingTaxRepository withholdingTaxRepository,
        IWithholdingTaxService withholdingTaxService,
        IWithholdingFiscalYearParameterRepository fiscalYearParameters,
        ISupplierInvoiceNumberService supplierInvoiceNumberService)
    {
        _purchaseReceiptRepository = purchaseReceiptRepository;
        _purchaseOrderRepository = purchaseOrderRepository;
        _supplierInvoiceRepository = supplierInvoiceRepository;
        _auditService = auditService;
        _logger = logger;
        _publisher = publisher;
        _withholdingTaxRepository = withholdingTaxRepository;
        _withholdingTaxService = withholdingTaxService;
        _fiscalYearParameters = fiscalYearParameters;
        _supplierInvoiceNumberService = supplierInvoiceNumberService;
    }

    public async Task<Result<SupplierInvoiceCreationResult>> Handle(
        CreateSupplierInvoiceFromPurchaseReceiptCommand request,
        CancellationToken cancellationToken)
    {
        var receipt = await _purchaseReceiptRepository.GetByIdWithLinesAsync(request.PurchaseReceiptId, cancellationToken);
        if (receipt is null)
            return Result.Failure<SupplierInvoiceCreationResult>(Error.NotFound("PurchaseReceipt", request.PurchaseReceiptId));

        if (!receipt.HasReceivedNotInvoiced)
            return Result.Failure<SupplierInvoiceCreationResult>(Error.Validation("Lines",
                "Aucune quantité reçue non facturée sur ce bon de réception"));

        if (receipt.PurchaseOrderId is not { } purchaseOrderId)
            return Result.Failure<SupplierInvoiceCreationResult>(Error.Validation("PurchaseOrder",
                "Ce bon de réception n'est pas lié à un bon de commande"));

        var po = await _purchaseOrderRepository.GetByIdWithLinesAsync(purchaseOrderId, cancellationToken);
        if (po is null)
            return Result.Failure<SupplierInvoiceCreationResult>(Error.NotFound("PurchaseOrder", purchaseOrderId));

        var trimmedNumber = await SupplierInvoiceNumberResolver.ResolveAsync(
            request.InvoiceNumber,
            request.UseSuggestedNumber,
            request.InvoiceDate,
            _supplierInvoiceNumberService,
            cancellationToken);
        if (trimmedNumber.IsFailure)
            return Result.Failure<SupplierInvoiceCreationResult>(trimmedNumber.Error);

        var invoiceNumber = trimmedNumber.Value;

        var lineSelections = SupplierInvoiceCreationHelper.ResolvePurchaseReceiptLineSelections(receipt, request.Lines);
        if (lineSelections.Count == 0)
            return Result.Failure<SupplierInvoiceCreationResult>(Error.Validation("Lines", "Sélectionnez au moins une ligne à facturer"));

        var result = SupplierInvoice.CreateFromPurchaseReceipt(
            receipt,
            po,
            invoiceNumber,
            request.InvoiceDate,
            lineSelections,
            request.PaymentTermDays,
            request.ExternalReference,
            request.Notes,
            request.PaymentMethod);

        if (result.IsFailure)
            return Result.Failure<SupplierInvoiceCreationResult>(result.Error);

        var invoice = result.Value;

        if (request.LineAssetClassifications is { Count: > 0 })
        {
            invoice.ApplyLineAssetClassifications(
                request.LineAssetClassifications
                    .Select(c => (c.LineNumber, c.IsFixedAsset, c.AssetAccountNumber, c.DepreciationRateCategoryId))
                    .ToList());
        }

        var numberWasAutoResolved = request.UseSuggestedNumber
            || string.IsNullOrWhiteSpace(request.InvoiceNumber);

        return await SupplierInvoiceCreationHelper.PersistAndFinalizeAsync(
            invoice,
            po,
            receipt,
            request.SendEmail,
            _auditService,
            _supplierInvoiceRepository,
            _purchaseOrderRepository,
            _purchaseReceiptRepository,
            _withholdingTaxRepository,
            _withholdingTaxService,
            _fiscalYearParameters,
            _publisher,
            _logger,
            _supplierInvoiceNumberService,
            numberWasAutoResolved,
            cancellationToken);
    }
}
