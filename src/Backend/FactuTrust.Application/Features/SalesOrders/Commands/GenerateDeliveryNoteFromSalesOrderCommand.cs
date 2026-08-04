using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services;
using FactuTrust.Domain.ValueObjects;
using MediatR;

namespace FactuTrust.Application.Features.SalesOrders.Commands;

/// <summary>Quantité à livrer sur une ligne de commande donnée.</summary>
public sealed record SalesOrderDeliveryLineDto(Guid SalesOrderLineId, decimal Quantity);

/// <summary>
/// Émet un bon de livraison depuis une commande client.
///
/// Ne peut porter que sur le RESTE À LIVRER : c'est ce qui permet d'émettre plusieurs bons
/// successifs rattachés au même engagement, là où un bon isolé perdait son reliquat.
/// Quand aucune ligne n'est précisée, le bon reprend l'intégralité du reste à livrer.
/// </summary>
public sealed record GenerateDeliveryNoteFromSalesOrderCommand(
    Guid SalesOrderId,
    IReadOnlyList<SalesOrderDeliveryLineDto>? Lines = null,
    DateTime? IssueDate = null,
    string? DeliveryAddress = null) : IRequest<Result<Guid>>;

public sealed class GenerateDeliveryNoteFromSalesOrderCommandHandler
    : IRequestHandler<GenerateDeliveryNoteFromSalesOrderCommand, Result<Guid>>
{
    private readonly ISalesOrderRepository _salesOrderRepository;
    private readonly IDeliveryNoteRepository _deliveryNoteRepository;
    private readonly IProductRepository _productRepository;
    private readonly IDocumentNumberService _documentNumberService;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;

    public GenerateDeliveryNoteFromSalesOrderCommandHandler(
        ISalesOrderRepository salesOrderRepository,
        IDeliveryNoteRepository deliveryNoteRepository,
        IProductRepository productRepository,
        IDocumentNumberService documentNumberService,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IAuditService auditService)
    {
        _salesOrderRepository = salesOrderRepository;
        _deliveryNoteRepository = deliveryNoteRepository;
        _productRepository = productRepository;
        _documentNumberService = documentNumberService;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<Result<Guid>> Handle(
        GenerateDeliveryNoteFromSalesOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _salesOrderRepository.GetByIdWithLinesAsync(request.SalesOrderId, cancellationToken);
        if (order is null)
            return Result.Failure<Guid>(Error.NotFound("Commande", request.SalesOrderId));

        if (!order.Status.CanBeDelivered())
            return Result.Failure<Guid>(Error.Validation("Status",
                $"Cette commande ne peut pas être livrée dans son état actuel ({order.Status.ToDisplayString()})"));

        // Sélection : lignes demandées, ou tout le reste à livrer.
        var requested = ResolveRequestedLines(order, request.Lines);
        if (requested.Count == 0)
            return Result.Failure<Guid>(Error.Validation("Lines", "Aucune quantité à livrer sur cette commande"));

        foreach (var (line, quantity) in requested)
        {
            if (quantity > line.PendingDeliveryQuantity)
            {
                return Result.Failure<Guid>(Error.Validation("Quantity",
                    $"La ligne {line.LineNumber} « {line.ProductName} » n'a plus que " +
                    $"{line.PendingDeliveryQuantity} à livrer (demandé : {quantity})"));
            }
        }

        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Aucun contexte d'entreprise disponible.");

        var issueDate = request.IssueDate?.Date ?? DateTime.UtcNow.Date;

        var docResult = await _documentNumberService.ReserveNextAsync(
            tenantId, NumberingDocumentType.DeliveryNote, issueDate.Year, issueDate, cancellationToken);

        var number = DocumentNumberMapperBridge.ToDeliveryNoteNumber(docResult);

        var address = !string.IsNullOrWhiteSpace(request.DeliveryAddress)
            ? request.DeliveryAddress!
            : order.Client.Address.ToSingleLine();

        var noteResult = DeliveryNote.Create(
            number,
            order.Client,
            issueDate,
            address,
            reference: $"Commande {order.Number.Value}",
            notes: order.Notes,
            warehouseId: order.WarehouseId);

        if (noteResult.IsFailure)
            return Result.Failure<Guid>(noteResult.Error);

        var deliveryNote = noteResult.Value;
        deliveryNote.AttachSalesOrderOrigin(order.Id);

        foreach (var (line, quantity) in requested)
        {
            var product = line.Product
                ?? await _productRepository.GetByIdAsync(line.ProductId, cancellationToken);

            if (product is null)
                return Result.Failure<Guid>(Error.NotFound("Produit", line.ProductId));

            // Remise et taux FODEC repris de la commande : le bon doit annoncer les mêmes
            // montants que l'engagement, et la facture qui en découlera aussi.
            var addResult = deliveryNote.AddLine(
                product,
                quantity,
                notes: line.Notes,
                discountPercent: line.DiscountPercent,
                fodecRatePercent: line.FodecRatePercent,
                unitPriceOverride: line.UnitPrice,
                appliedPromotionId: line.AppliedPromotionId,
                appliedPromotionName: line.AppliedPromotionName);

            if (addResult.IsFailure)
                return Result.Failure<Guid>(addResult.Error);
        }

        deliveryNote.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system");
        await _deliveryNoteRepository.AddAsync(deliveryNote, cancellationToken);

        await _auditService.LogAsync(
            AuditActions.DeliveryNote.Created,
            "DeliveryNote",
            deliveryNote.Id,
            newValues: new
            {
                deliveryNote.Number.Value,
                SourceSalesOrderId = order.Id,
                SourceSalesOrderNumber = order.Number.Value
            },
            cancellationToken: cancellationToken);

        return Result.Success(deliveryNote.Id);
    }

    private static List<(SalesOrderLine Line, decimal Quantity)> ResolveRequestedLines(
        SalesOrder order,
        IReadOnlyList<SalesOrderDeliveryLineDto>? requested)
    {
        if (requested is null || requested.Count == 0)
        {
            return order.Lines
                .Where(l => l.PendingDeliveryQuantity > 0)
                .OrderBy(l => l.LineNumber)
                .Select(l => (l, l.PendingDeliveryQuantity))
                .ToList();
        }

        var result = new List<(SalesOrderLine, decimal)>();
        foreach (var item in requested.Where(r => r.Quantity > 0))
        {
            var line = order.Lines.FirstOrDefault(l => l.Id == item.SalesOrderLineId);
            if (line is not null)
                result.Add((line, item.Quantity));
        }

        return result.OrderBy(x => x.Item1.LineNumber).ToList();
    }
}

/// <summary>
/// Pont vers le mappeur de numérotation, qui vit dans la couche Infrastructure.
/// Évite d'y créer une dépendance depuis Application pour une seule conversion de format.
/// </summary>
internal static class DocumentNumberMapperBridge
{
    public static DeliveryNoteNumber ToDeliveryNoteNumber(DocumentNumberResult result) =>
        DeliveryNoteNumber.FromRendered(result.Value, result.Year, result.Sequence);
}
