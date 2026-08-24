using FactuTrust.Application.DTOs;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Features.Stock.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.Invoices.Commands;

/// <summary>
/// Command to validate an invoice (finalize draft).
/// </summary>
public sealed record ValidateInvoiceCommand(
    Guid InvoiceId,
    IReadOnlyList<DocumentLineAllocationsDto>? LineAllocations = null) : IRequest<Result>;

/// <summary>
/// Handler for ValidateInvoiceCommand.
/// Transaction stricte : la facture et son écriture comptable sont persistées dans la MÊME
/// transaction (unité de travail ambiante). Si la génération de l'écriture échoue (compte
/// manquant, déséquilibre…), la validation est refusée et la facture reste en brouillon.
/// Un tenant sans plan comptable initialisé reste validable (la génération est alors un
/// succès « non applicable » — comportement historique préservé).
/// </summary>
public sealed class ValidateInvoiceCommandHandler : IRequestHandler<ValidateInvoiceCommand, Result>
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IAccountingService _accountingService;
    private readonly ITenantUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;
    private readonly IInvoiceComplianceValidator _complianceValidator;
    private readonly ITrackedDocumentStockService _trackedStock;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IProductRepository _productRepository;
    private readonly IStockAllocationValidator _allocationValidator;
    private readonly IStockMovementRepository _stockMovementRepository;
    private readonly IStockItemRepository _stockItemRepository;

    public ValidateInvoiceCommandHandler(
        IInvoiceRepository invoiceRepository,
        IAccountingService accountingService,
        ITenantUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IAuditService auditService,
        IInvoiceComplianceValidator complianceValidator,
        ITrackedDocumentStockService trackedStock,
        IWarehouseRepository warehouseRepository,
        IProductRepository productRepository,
        IStockAllocationValidator allocationValidator,
        IStockMovementRepository stockMovementRepository,
        IStockItemRepository stockItemRepository)
    {
        _invoiceRepository = invoiceRepository;
        _accountingService = accountingService;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _auditService = auditService;
        _complianceValidator = complianceValidator;
        _trackedStock = trackedStock;
        _warehouseRepository = warehouseRepository;
        _productRepository = productRepository;
        _allocationValidator = allocationValidator;
        _stockMovementRepository = stockMovementRepository;
        _stockItemRepository = stockItemRepository;
    }

    public async Task<Result> Handle(ValidateInvoiceCommand request, CancellationToken cancellationToken)
    {
        var result = await _unitOfWork.ExecuteAsync(async ct =>
        {
            var invoice = await _invoiceRepository.GetByIdWithLinesAsync(request.InvoiceId, ct);

            if (invoice is null)
                return Result.Failure(Error.NotFound("Facture", request.InvoiceId));

            // Conformité fiscale : ce handler est le point de passage unique de tous les
            // chemins hors assistant (création directe, conversion devis, conversion BL).
            // L'assistant conserve sa propre validation, plus riche, au niveau du brouillon.
            var compliance = await _complianceValidator.ValidateInvoiceAsync(invoice, ct);
            if (!compliance.CanProceed)
            {
                var blocking = compliance.Checks
                    .Where(c => c.Status == "ERROR" && c.IsBlocking)
                    .Select(c => $"{c.Label} : {c.Description}")
                    .ToList();

                return Result.Failure(Error.Validation(
                    "Compliance",
                    blocking.Count > 0
                        ? $"Facture non conforme — {string.Join(" ; ", blocking)}"
                        : "Facture non conforme"));
            }

            if (invoice.Type == InvoiceType.CreditNote)
            {
                var restoreResult = await CreditNoteStockRestore.RestoreAsync(
                    invoice,
                    _invoiceRepository,
                    _productRepository,
                    _warehouseRepository,
                    _stockMovementRepository,
                    _stockItemRepository,
                    _trackedStock,
                    ct);
                if (restoreResult.IsFailure)
                    return restoreResult;
            }
            else if (!invoice.SourceDeliveryNoteId.HasValue)
            {
                var stockResult = await DeductTrackedStockAsync(invoice, request.LineAllocations, ct);
                if (stockResult.IsFailure)
                    return stockResult;
            }

            var validateResult = invoice.Validate();

            if (validateResult.IsFailure)
                return validateResult;

            invoice.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system", isUpdate: true);

            await _invoiceRepository.UpdateAsync(invoice, ct);

            var entryResult = invoice.Type == InvoiceType.CreditNote
                ? await _accountingService.GenerateInvoiceCreditNoteEntryAsync(invoice, ct)
                : await _accountingService.GenerateInvoiceSaleEntryAsync(invoice, ct);

            if (entryResult.IsFailure)
                return Result.Failure(entryResult.Error);

            return Result.Success();
        }, cancellationToken);

        if (result.IsFailure)
            return result;

        // Audit hors transaction (chaîne hash-chaînée dans sa propre transaction Serializable).
        await _auditService.LogAsync(
            AuditActions.Invoice.Validated,
            "Invoice",
            request.InvoiceId,
            cancellationToken: cancellationToken);

        return result;
    }

    private async Task<Result> DeductTrackedStockAsync(
        Invoice invoice,
        IReadOnlyList<DocumentLineAllocationsDto>? lineAllocations,
        CancellationToken cancellationToken)
    {
        var allocationsByLine = (lineAllocations ?? Array.Empty<DocumentLineAllocationsDto>())
            .ToDictionary(a => a.LineId, a => a.Allocations);

        var lines = new List<TrackedDocumentLine>();
        var anyTracked = false;
        foreach (var line in invoice.Lines)
        {
            if (!line.ProductId.HasValue)
                continue;

            var product = await _productRepository.GetByIdAsync(line.ProductId.Value, cancellationToken);
            if (product is null || !product.IsStockManaged)
                continue;

            if (_trackedStock.IsLiveTracked(product))
                anyTracked = true;

            var allocations = allocationsByLine.GetValueOrDefault(line.Id);
            var validateAlloc = await _allocationValidator.ValidateExitAllocationsAsync(
                line.ProductId.Value, line.Quantity, allocations, cancellationToken);
            if (validateAlloc.IsFailure)
                return validateAlloc;

            lines.Add(new TrackedDocumentLine(
                line.ProductId.Value,
                line.Quantity,
                line.Id,
                StockDocumentKind.Invoice,
                allocations));
        }

        if (!anyTracked)
            return Result.Success();

        Warehouse? warehouse = null;
        if (invoice.WarehouseId.HasValue)
            warehouse = await _warehouseRepository.GetByIdAsync(invoice.WarehouseId.Value, cancellationToken);
        warehouse ??= await _warehouseRepository.GetDefaultAsync(cancellationToken);
        if (warehouse is null)
            return Result.Failure(Error.Validation("Warehouse",
                "Un entrepôt est obligatoire pour valider une facture contenant des articles suivis."));

        return await _trackedStock.ApplyExitsAsync(
            warehouse.Id,
            $"Facture {invoice.Number.Value}",
            MovementReason.Sale,
            lines,
            cancellationToken,
            includeUntracked: true);
    }
}
