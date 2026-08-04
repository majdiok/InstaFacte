using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Features.Accounting.Notifications;
using FactuTrust.Application.Features.SupplierInvoices.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Application.Features.PurchaseOrders.Commands;

public sealed record CreateSupplierInvoiceFromPOCommand(
    Guid PurchaseOrderId,
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

public sealed class CreateSupplierInvoiceFromPOCommandHandler
    : IRequestHandler<CreateSupplierInvoiceFromPOCommand, Result<SupplierInvoiceCreationResult>>
{
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly ISupplierInvoiceRepository _supplierInvoiceRepository;
    private readonly IAuditService _auditService;
    private readonly ILogger<CreateSupplierInvoiceFromPOCommandHandler> _logger;
    private readonly IPublisher _publisher;
    private readonly IWithholdingTaxRepository _withholdingTaxRepository;
    private readonly IWithholdingTaxService _withholdingTaxService;
    private readonly IWithholdingFiscalYearParameterRepository _fiscalYearParameters;
    private readonly ISupplierInvoiceNumberService _supplierInvoiceNumberService;

    public CreateSupplierInvoiceFromPOCommandHandler(
        IPurchaseOrderRepository purchaseOrderRepository,
        ISupplierInvoiceRepository supplierInvoiceRepository,
        IAuditService auditService,
        ILogger<CreateSupplierInvoiceFromPOCommandHandler> logger,
        IPublisher publisher,
        IWithholdingTaxRepository withholdingTaxRepository,
        IWithholdingTaxService withholdingTaxService,
        IWithholdingFiscalYearParameterRepository fiscalYearParameters,
        ISupplierInvoiceNumberService supplierInvoiceNumberService)
    {
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

    public async Task<Result<SupplierInvoiceCreationResult>> Handle(CreateSupplierInvoiceFromPOCommand request, CancellationToken cancellationToken)
    {
        var po = await _purchaseOrderRepository.GetByIdWithLinesAsync(request.PurchaseOrderId, cancellationToken);
        if (po is null)
            return Result.Failure<SupplierInvoiceCreationResult>(Error.NotFound("PurchaseOrder", request.PurchaseOrderId));

        if (!po.HasReceivedNotInvoiced)
            return Result.Failure<SupplierInvoiceCreationResult>(Error.Validation("Lines",
                "Aucune quantité reçue non facturée sur ce bon de commande"));

        var invoiceNumberResult = await SupplierInvoiceNumberResolver.ResolveAsync(
            request.InvoiceNumber,
            request.UseSuggestedNumber,
            request.InvoiceDate,
            _supplierInvoiceNumberService,
            cancellationToken);
        if (invoiceNumberResult.IsFailure)
            return Result.Failure<SupplierInvoiceCreationResult>(invoiceNumberResult.Error);

        var invoiceNumber = invoiceNumberResult.Value;

        var lineSelections = SupplierInvoiceCreationHelper.ResolvePurchaseOrderLineSelections(po, request.Lines);
        if (lineSelections.Count == 0)
            return Result.Failure<SupplierInvoiceCreationResult>(Error.Validation("Lines", "Sélectionnez au moins une ligne à facturer"));

        var result = SupplierInvoice.CreateFromPurchaseOrder(
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
            purchaseReceipt: null,
            request.SendEmail,
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
}
