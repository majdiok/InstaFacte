using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Stock.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FluentValidation;
using MediatR;

namespace FactuTrust.Application.Features.DeliveryNotes.Commands;

/// <summary>
/// Command to record delivery completion.
/// </summary>
public sealed record RecordDeliveryCommand(
    Guid DeliveryNoteId,
    RecordDeliveryDto Delivery,
    IReadOnlyList<DocumentLineAllocationsDto>? LineAllocations = null) : IRequest<Result>;

public sealed class RecordDeliveryCommandValidator : AbstractValidator<RecordDeliveryCommand>
{
    public RecordDeliveryCommandValidator()
    {
        RuleFor(x => x.DeliveryNoteId)
            .NotEmpty()
            .WithMessage("L'identifiant du bon de livraison est obligatoire");

        RuleFor(x => x.Delivery.DeliveryDate)
            .NotEmpty()
            .WithMessage("La date de livraison est obligatoire");

        RuleFor(x => x.Delivery.RecipientName)
            .NotEmpty()
            .WithMessage("Le nom du réceptionnaire est obligatoire")
            .MaximumLength(200)
            .WithMessage("Le nom du réceptionnaire ne peut pas dépasser 200 caractères");
    }
}

public sealed class RecordDeliveryCommandHandler : IRequestHandler<RecordDeliveryCommand, Result>
{
    private readonly IDeliveryNoteRepository _deliveryNoteRepository;
    private readonly ITenantUnitOfWork _unitOfWork;
    private readonly IAuditService _auditService;
    private readonly ITrackedDocumentStockService _trackedStock;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IProductRepository _productRepository;
    private readonly IStockAllocationValidator _allocationValidator;

    public RecordDeliveryCommandHandler(
        IDeliveryNoteRepository deliveryNoteRepository,
        ITenantUnitOfWork unitOfWork,
        IAuditService auditService,
        ITrackedDocumentStockService trackedStock,
        IWarehouseRepository warehouseRepository,
        IProductRepository productRepository,
        IStockAllocationValidator allocationValidator)
    {
        _deliveryNoteRepository = deliveryNoteRepository;
        _unitOfWork = unitOfWork;
        _auditService = auditService;
        _trackedStock = trackedStock;
        _warehouseRepository = warehouseRepository;
        _productRepository = productRepository;
        _allocationValidator = allocationValidator;
    }

    public async Task<Result> Handle(RecordDeliveryCommand request, CancellationToken cancellationToken)
    {
        var result = await _unitOfWork.ExecuteAsync(async ct =>
        {
            var deliveryNote = await _deliveryNoteRepository.GetByIdWithLinesAsync(request.DeliveryNoteId, ct);
            if (deliveryNote is null)
                return Result.Failure(Error.NotFound("DeliveryNote", request.DeliveryNoteId));

            var dto = request.Delivery;

            if (dto.Lines != null && dto.Lines.Count > 0)
            {
                foreach (var lineDto in dto.Lines)
                {
                    var line = deliveryNote.Lines.FirstOrDefault(l => l.Id == lineDto.LineId);
                    if (line is null)
                        return Result.Failure(Error.NotFound("DeliveryNoteLine", lineDto.LineId));

                    var recordResult = line.RecordDelivery(
                        lineDto.DeliveredQuantity,
                        lineDto.RejectedQuantity,
                        lineDto.RejectionReason);

                    if (recordResult.IsFailure)
                        return recordResult;
                }
            }
            else
            {
                foreach (var line in deliveryNote.Lines)
                {
                    var recordResult = line.RecordDelivery(line.OrderedQuantity, 0, null);
                    if (recordResult.IsFailure)
                        return recordResult;
                }
            }

            var stockResult = await DeductTrackedStockAsync(deliveryNote, request.LineAllocations, ct);
            if (stockResult.IsFailure)
                return stockResult;

            var deliver = deliveryNote.RecordDelivery(
                dto.DeliveryDate,
                dto.RecipientName,
                dto.RecipientSignature);

            if (deliver.IsFailure)
                return deliver;

            await _deliveryNoteRepository.UpdateAsync(deliveryNote, ct);
            return Result.Success();
        }, cancellationToken);

        if (result.IsFailure)
            return result;

        try
        {
            await _auditService.LogAsync(
                AuditActions.DeliveryNote.Delivered,
                "DeliveryNote",
                request.DeliveryNoteId,
                cancellationToken: cancellationToken);
        }
        catch { }

        return Result.Success();
    }

    private async Task<Result> DeductTrackedStockAsync(
        DeliveryNote deliveryNote,
        IReadOnlyList<DocumentLineAllocationsDto>? lineAllocations,
        CancellationToken cancellationToken)
    {
        var allocationsByLine = (lineAllocations ?? Array.Empty<DocumentLineAllocationsDto>())
            .ToDictionary(a => a.LineId, a => a.Allocations);

        var lines = new List<TrackedDocumentLine>();
        var anyTracked = false;
        foreach (var line in deliveryNote.Lines)
        {
            if (line.DeliveredQuantity <= 0)
                continue;

            var product = await _productRepository.GetByIdAsync(line.ProductId, cancellationToken);
            if (product is null || !product.IsStockManaged)
                continue;

            if (_trackedStock.IsLiveTracked(product))
                anyTracked = true;

            var allocations = allocationsByLine.GetValueOrDefault(line.Id);
            var validateAlloc = await _allocationValidator.ValidateExitAllocationsAsync(
                line.ProductId, line.DeliveredQuantity, allocations, cancellationToken);
            if (validateAlloc.IsFailure)
                return validateAlloc;

            lines.Add(new TrackedDocumentLine(
                line.ProductId,
                line.DeliveredQuantity,
                line.Id,
                StockDocumentKind.DeliveryNote,
                allocations));
        }

        if (!anyTracked)
            return Result.Success();

        Warehouse? warehouse = null;
        if (deliveryNote.WarehouseId.HasValue)
            warehouse = await _warehouseRepository.GetByIdAsync(deliveryNote.WarehouseId.Value, cancellationToken);
        warehouse ??= await _warehouseRepository.GetDefaultAsync(cancellationToken);
        if (warehouse is null)
            return Result.Failure(Error.Validation("Warehouse",
                "Un entrepôt est obligatoire pour livrer des articles suivis."));

        return await _trackedStock.ApplyExitsAsync(
            warehouse.Id,
            $"BL {deliveryNote.Number.Value}",
            MovementReason.Delivery,
            lines,
            cancellationToken,
            includeUntracked: true);
    }
}
