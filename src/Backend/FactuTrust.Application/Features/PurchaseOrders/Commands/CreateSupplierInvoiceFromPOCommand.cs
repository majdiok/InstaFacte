using FactuTrust.Application.Common;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Features.Accounting.Notifications;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Application.Features.PurchaseOrders.Commands;

/// <summary>
/// Command to create a supplier invoice from a received purchase order.
/// </summary>
public sealed record SupplierInvoiceLineAssetRequest(
    int LineNumber,
    bool IsFixedAsset,
    Guid? DepreciationRateCategoryId = null,
    string? AssetAccountNumber = null);

public sealed record CreateSupplierInvoiceFromPOCommand(
    Guid PurchaseOrderId,
    string InvoiceNumber,
    DateTime InvoiceDate,
    int PaymentTermDays = 30,
    string? ExternalReference = null,
    string? Notes = null,
    bool SendEmail = false,
    IReadOnlyList<SupplierInvoiceLineAssetRequest>? LineAssetClassifications = null,
    string? PaymentMethod = null
) : IRequest<Result<Guid>>;

/// <summary>
/// Handler for CreateSupplierInvoiceFromPOCommand.
/// </summary>
public sealed class CreateSupplierInvoiceFromPOCommandHandler
    : IRequestHandler<CreateSupplierInvoiceFromPOCommand, Result<Guid>>
{
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly ISupplierInvoiceRepository _supplierInvoiceRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditService _auditService;
    private readonly IEmailService _emailService;
    private readonly ILogger<CreateSupplierInvoiceFromPOCommandHandler> _logger;
    private readonly IPublisher _publisher;
    private readonly IWithholdingTaxRepository _withholdingTaxRepository;
    private readonly IWithholdingTaxService _withholdingTaxService;
    private readonly IWithholdingFiscalYearParameterRepository _fiscalYearParameters;

    public CreateSupplierInvoiceFromPOCommandHandler(
        IPurchaseOrderRepository purchaseOrderRepository,
        ISupplierInvoiceRepository supplierInvoiceRepository,
        IUnitOfWork unitOfWork,
        IAuditService auditService,
        IEmailService emailService,
        ILogger<CreateSupplierInvoiceFromPOCommandHandler> logger,
        IPublisher publisher,
        IWithholdingTaxRepository withholdingTaxRepository,
        IWithholdingTaxService withholdingTaxService,
        IWithholdingFiscalYearParameterRepository fiscalYearParameters)
    {
        _purchaseOrderRepository = purchaseOrderRepository;
        _supplierInvoiceRepository = supplierInvoiceRepository;
        _unitOfWork = unitOfWork;
        _auditService = auditService;
        _emailService = emailService;
        _logger = logger;
        _publisher = publisher;
        _withholdingTaxRepository = withholdingTaxRepository;
        _withholdingTaxService = withholdingTaxService;
        _fiscalYearParameters = fiscalYearParameters;
    }

    public async Task<Result<Guid>> Handle(CreateSupplierInvoiceFromPOCommand request, CancellationToken cancellationToken)
    {
        // Verify PO exists with lines
        var po = await _purchaseOrderRepository.GetByIdWithLinesAsync(request.PurchaseOrderId, cancellationToken);
        if (po is null)
            return Result.Failure<Guid>(Error.NotFound("PurchaseOrder", request.PurchaseOrderId));

        // Check if a supplier invoice already exists for this PO
        var exists = await _supplierInvoiceRepository.ExistsForPurchaseOrderAsync(request.PurchaseOrderId, cancellationToken);
        if (exists)
            return Result.Failure<Guid>(Error.Conflict(
                "Une facture fournisseur existe déjà pour ce bon de commande."));

        // Check invoice number uniqueness across all supplier invoices
        var trimmedNumber = request.InvoiceNumber.Trim();
        var numberExists = await _supplierInvoiceRepository.ExistsByInvoiceNumberAsync(trimmedNumber, cancellationToken);
        if (numberExists)
            return Result.Failure<Guid>(Error.Conflict(
                $"Le numéro de facture '{trimmedNumber}' existe déjà. Veuillez en choisir un autre."));

        // Create the supplier invoice
        var result = SupplierInvoice.CreateFromPurchaseOrder(
            po,
            request.InvoiceNumber,
            request.InvoiceDate,
            request.PaymentTermDays,
            request.ExternalReference,
            request.Notes,
            request.PaymentMethod);

        if (result.IsFailure)
            return Result.Failure<Guid>(result.Error);

        var invoice = result.Value;

        if (request.LineAssetClassifications is { Count: > 0 })
        {
            invoice.ApplyLineAssetClassifications(
                request.LineAssetClassifications
                    .Select(c => (c.LineNumber, c.IsFixedAsset, c.AssetAccountNumber, c.DepreciationRateCategoryId))
                    .ToList());
        }

        await SupplierInvoiceWithholdingComputation.ApplyWithholdingPreviewAsync(
            invoice,
            _withholdingTaxRepository,
            _withholdingTaxService,
            _fiscalYearParameters,
            cancellationToken);
        
        // Mark PO as invoiced
        var invoiceResult = po.MarkAsInvoiced(invoice.Id);
        if (invoiceResult.IsFailure)
        {
            // If we can't mark as invoiced, we should probably fail the whole operation
            // or at least log it. Since status check is done inside MarkAsInvoiced,
            // this should only fail if state changed concurrently.
             return Result.Failure<Guid>(invoiceResult.Error);
        }

        try
        {
            await _supplierInvoiceRepository.AddAsync(invoice, cancellationToken);
        }
        catch (Exception ex) when (IsDuplicateKeyException(ex))
        {
            _logger.LogWarning(ex, "Duplicate invoice number detected at DB level for {InvoiceNumber}", invoice.InvoiceNumber);
            return Result.Failure<Guid>(Error.Conflict(
                $"Le numéro de facture '{invoice.InvoiceNumber}' existe déjà. Veuillez en choisir un autre."));
        }

        // Explicitly persist the PO status change (Status = Invoiced, InvoicedAt).
        // Each repository creates its own DbContext, so MarkAsInvoiced() only modifies
        // the in-memory object. Without this call, the PO status is never saved to the DB.
        await _purchaseOrderRepository.UpdateAsync(po, cancellationToken);

        // Send email if requested
        if (request.SendEmail)
        {
            try
            {
                // TODO: Implement supplier invoice email sending
                // For now, just log that email was requested
                _logger.LogInformation("Email sending requested for supplier invoice {InvoiceNumber}", invoice.InvoiceNumber);
            }
            catch (Exception ex)
            {
                // Log error but don't fail the operation
                _logger.LogWarning(ex, "Failed to send supplier invoice email for invoice {InvoiceNumber}", invoice.InvoiceNumber);
            }
        }

        await _auditService.LogAsync(
            AuditActions.SupplierInvoice.Created,
            "SupplierInvoice",
            invoice.Id,
            newValues: new
            {
                invoice.InvoiceNumber,
                PurchaseOrderNumber = po.Number.Value,
                TotalAmount = po.TotalAmount.Amount
            },
            cancellationToken: cancellationToken);

        await _publisher.Publish(new SupplierInvoiceCreatedForAccountingNotification(invoice.Id), cancellationToken);

        return Result.Success(invoice.Id);
    }

    private static bool IsDuplicateKeyException(Exception ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("IX_SupplierInvoices_InvoiceNumber")
               || message.Contains("UNIQUE constraint")
               || message.Contains("duplicate key")
               || message.Contains("Cannot insert duplicate");
    }
}
