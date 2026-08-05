using FactuTrust.Application.Common;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Features.Accounting.Notifications;
using FactuTrust.Application.Features.PurchaseOrders.Commands;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FactuTrust.Application.Features.SupplierInvoices.Services;

/// <summary>
/// Shared post-creation steps for supplier invoices from purchase documents.
/// </summary>
internal static class SupplierInvoiceCreationHelper
{
    private const int MaxDuplicateRetryAttempts = 3;
    private const string InvoiceNumberIndexName = "IX_SupplierInvoices_InvoiceNumber";

    public static async Task<Result<SupplierInvoiceCreationResult>> PersistAndFinalizeAsync(
        SupplierInvoice invoice,
        PurchaseOrder? purchaseOrder,
        PurchaseReceipt? purchaseReceipt,
        bool sendEmail,
        IAuditService auditService,
        ISupplierInvoiceRepository supplierInvoiceRepository,
        IPurchaseOrderRepository purchaseOrderRepository,
        IPurchaseReceiptRepository? purchaseReceiptRepository,
        IWithholdingTaxRepository withholdingTaxRepository,
        IWithholdingTaxService withholdingTaxService,
        IWithholdingFiscalYearParameterRepository fiscalYearParameters,
        IPublisher publisher,
        ILogger logger,
        ISupplierInvoiceNumberService supplierInvoiceNumberService,
        bool numberWasAutoResolved,
        CancellationToken cancellationToken)
    {
        var poImputations = invoice.GetPurchaseOrderImputations();
        if (purchaseOrder is not null && poImputations.Count > 0)
        {
            var poResult = purchaseOrder.ApplyInvoicing(poImputations, invoice.Id);
            if (poResult.IsFailure)
                return Result.Failure<SupplierInvoiceCreationResult>(poResult.Error);
        }

        if (purchaseReceipt is not null)
        {
            var prImputations = invoice.GetPurchaseReceiptImputations();
            if (prImputations.Count > 0)
            {
                var prResult = purchaseReceipt.ApplyInvoicing(prImputations);
                if (prResult.IsFailure)
                    return Result.Failure<SupplierInvoiceCreationResult>(prResult.Error);
            }
        }

        await SupplierInvoiceWithholdingComputation.ApplyWithholdingPreviewAsync(
            invoice,
            withholdingTaxRepository,
            withholdingTaxService,
            fiscalYearParameters,
            cancellationToken);

        var persistResult = await PersistWithDuplicateRetryAsync(
            invoice,
            supplierInvoiceRepository,
            supplierInvoiceNumberService,
            numberWasAutoResolved,
            logger,
            cancellationToken);
        if (persistResult.IsFailure)
            return Result.Failure<SupplierInvoiceCreationResult>(persistResult.Error);

        if (purchaseOrder is not null)
            await purchaseOrderRepository.UpdateAsync(purchaseOrder, cancellationToken);

        if (purchaseReceipt is not null && purchaseReceiptRepository is not null)
            await purchaseReceiptRepository.UpdateAsync(purchaseReceipt, cancellationToken);

        if (sendEmail)
            logger.LogInformation("Email sending requested for supplier invoice {InvoiceNumber}", invoice.InvoiceNumber);

        await auditService.LogAsync(
            AuditActions.SupplierInvoice.Created,
            "SupplierInvoice",
            invoice.Id,
            newValues: new
            {
                invoice.InvoiceNumber,
                PurchaseOrderNumber = purchaseOrder?.Number.Value,
                PurchaseReceiptNumber = purchaseReceipt?.Number.Value,
                TotalAmount = invoice.TotalAmount.Amount
            },
            cancellationToken: cancellationToken);

        await publisher.Publish(new SupplierInvoiceCreatedForAccountingNotification(invoice.Id), cancellationToken);

        return Result.Success(new SupplierInvoiceCreationResult(invoice.Id, invoice.InvoiceNumber));
    }

    private static async Task<Result> PersistWithDuplicateRetryAsync(
        SupplierInvoice invoice,
        ISupplierInvoiceRepository supplierInvoiceRepository,
        ISupplierInvoiceNumberService supplierInvoiceNumberService,
        bool numberWasAutoResolved,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxDuplicateRetryAttempts; attempt++)
        {
            try
            {
                await supplierInvoiceRepository.AddAsync(invoice, cancellationToken);
                return Result.Success();
            }
            catch (Exception ex) when (IsDuplicateKeyException(ex))
            {
                logger.LogWarning(ex,
                    "Duplicate invoice number detected at DB level for {InvoiceNumber} (attempt {Attempt}/{Max}, autoResolved={AutoResolved}, dbError={DbError})",
                    invoice.InvoiceNumber, attempt, MaxDuplicateRetryAttempts, numberWasAutoResolved,
                    (ex.InnerException ?? ex).Message);

                if (!numberWasAutoResolved)
                {
                    var suggested = await TryPreviewAsync(supplierInvoiceNumberService, invoice.InvoiceDate, cancellationToken);
                    return BuildConflict(invoice.InvoiceNumber, suggested);
                }

                if (attempt == MaxDuplicateRetryAttempts)
                {
                    var suggested = await TryPreviewAsync(supplierInvoiceNumberService, invoice.InvoiceDate, cancellationToken);
                    return BuildConflict(invoice.InvoiceNumber, suggested);
                }

                var reserved = await supplierInvoiceNumberService.ReserveNextAsync(invoice.InvoiceDate, cancellationToken);
                var reassign = invoice.ReassignInvoiceNumberForRetry(reserved);
                if (reassign.IsFailure)
                    return reassign;

                logger.LogInformation(
                    "Reserved new supplier invoice number {NewInvoiceNumber} after duplicate at attempt {Attempt}",
                    invoice.InvoiceNumber, attempt);
            }
        }

        // Unreachable — the loop returns on every branch — but keeps the compiler happy.
        return BuildConflict(invoice.InvoiceNumber, null);
    }

    private static Result BuildConflict(string invoiceNumber, string? suggested)
    {
        var metadata = new Dictionary<string, object?>
        {
            ["suggestedInvoiceNumber"] = suggested,
            ["conflictingInvoiceNumber"] = invoiceNumber
        };
        return Result.Failure(Error.Conflict(
            $"Le numéro de facture '{invoiceNumber}' existe déjà. Veuillez en choisir un autre.",
            metadata));
    }

    private static async Task<string?> TryPreviewAsync(
        ISupplierInvoiceNumberService service,
        DateTime invoiceDate,
        CancellationToken cancellationToken)
    {
        try
        {
            return await service.PreviewNextAsync(invoiceDate, cancellationToken);
        }
        catch
        {
            return null;
        }
    }

    public static IReadOnlyList<(Guid LineId, decimal Quantity)> ResolvePurchaseOrderLineSelections(
        PurchaseOrder purchaseOrder,
        IReadOnlyList<CreateSupplierInvoiceLineSelection>? requestedLines)
    {
        if (requestedLines is { Count: > 0 })
        {
            return requestedLines
                .Where(l => l.QuantityToInvoice > 0)
                .Select(l => (l.SourceLineId, l.QuantityToInvoice))
                .ToList();
        }

        return purchaseOrder.Lines
            .Where(l => l.ReceivedNotInvoicedQuantity > 0)
            .Select(l => (l.Id, l.ReceivedNotInvoicedQuantity))
            .ToList();
    }

    public static IReadOnlyList<(Guid LineId, decimal Quantity)> ResolvePurchaseReceiptLineSelections(
        PurchaseReceipt receipt,
        IReadOnlyList<CreateSupplierInvoiceLineSelection>? requestedLines)
    {
        if (requestedLines is { Count: > 0 })
        {
            return requestedLines
                .Where(l => l.QuantityToInvoice > 0)
                .Select(l => (l.SourceLineId, l.QuantityToInvoice))
                .ToList();
        }

        return receipt.Lines
            .Where(l => l.ReceivedNotInvoicedQuantity > 0)
            .Select(l => (l.Id, l.ReceivedNotInvoicedQuantity))
            .ToList();
    }

    /// <summary>
    /// Ne reconnaît QUE la violation de l'index unique du numéro de facture fournisseur.
    /// Toute autre violation de clé (PK_Products, PK_PurchaseReceipts, ...) doit remonter
    /// telle quelle : la déguiser en conflit de numéro masque le vrai défaut.
    /// </summary>
    private static bool IsDuplicateKeyException(Exception ex)
    {
        for (Exception? e = ex; e is not null; e = e.InnerException)
        {
            if (e.Message.Contains(InvoiceNumberIndexName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}

public sealed record SupplierInvoiceCreationResult(Guid Id, string InvoiceNumber);

public sealed record CreateSupplierInvoiceLineSelection(Guid SourceLineId, decimal QuantityToInvoice);

public sealed record SupplierInvoiceLineAssetRequest(
    int LineNumber,
    bool IsFixedAsset,
    Guid? DepreciationRateCategoryId = null,
    string? AssetAccountNumber = null);
