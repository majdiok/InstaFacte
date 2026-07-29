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

/// <summary>Quantité à facturer sur une ligne de commande donnée.</summary>
public sealed record SalesOrderInvoiceLineDto(Guid SalesOrderLineId, decimal Quantity);

/// <summary>
/// Émet une facture DIRECTEMENT depuis une commande client, sans bon de livraison
/// intermédiaire — vente sur commande, facturation d'avance.
///
/// Deux assiettes possibles :
/// <list type="bullet">
///   <item>par défaut, le livré-non-facturé — la facturation suit la livraison ;</item>
///   <item>en facturation d'avance, le reste à facturer, indépendamment des livraisons.</item>
/// </list>
///
/// ⚠️ La facture produite ne porte PAS de <c>SourceDeliveryNoteId</c> : aucune sortie de
/// stock n'a eu lieu, sa validation doit donc bien déduire le stock.
/// </summary>
public sealed record GenerateInvoiceFromSalesOrderCommand(
    Guid SalesOrderId,
    IReadOnlyList<SalesOrderInvoiceLineDto>? Lines = null,
    DateTime? IssueDate = null,
    DateTime? DueDate = null,
    bool AdvanceBilling = false) : IRequest<Result<Guid>>;

public sealed class GenerateInvoiceFromSalesOrderCommandHandler
    : IRequestHandler<GenerateInvoiceFromSalesOrderCommand, Result<Guid>>
{
    private readonly ISalesOrderRepository _salesOrderRepository;
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IProductRepository _productRepository;
    private readonly IDocumentNumberService _documentNumberService;
    private readonly IFiscalStampResolver _fiscalStampResolver;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;

    public GenerateInvoiceFromSalesOrderCommandHandler(
        ISalesOrderRepository salesOrderRepository,
        IInvoiceRepository invoiceRepository,
        IProductRepository productRepository,
        IDocumentNumberService documentNumberService,
        IFiscalStampResolver fiscalStampResolver,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IAuditService auditService)
    {
        _salesOrderRepository = salesOrderRepository;
        _invoiceRepository = invoiceRepository;
        _productRepository = productRepository;
        _documentNumberService = documentNumberService;
        _fiscalStampResolver = fiscalStampResolver;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<Result<Guid>> Handle(
        GenerateInvoiceFromSalesOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _salesOrderRepository.GetByIdWithLinesAsync(request.SalesOrderId, cancellationToken);
        if (order is null)
            return Result.Failure<Guid>(Error.NotFound("Commande", request.SalesOrderId));

        if (!order.Status.CanBeInvoiced())
            return Result.Failure<Guid>(Error.Validation("Status",
                $"Cette commande ne peut pas être facturée dans son état actuel ({order.Status.ToDisplayString()})"));

        var requested = ResolveRequestedLines(order, request.Lines, request.AdvanceBilling);
        if (requested.Count == 0)
        {
            return Result.Failure<Guid>(Error.Validation("Lines",
                request.AdvanceBilling
                    ? "Aucune quantité à facturer sur cette commande"
                    : "Aucune quantité livrée non facturée — livrez d'abord, ou facturez d'avance"));
        }

        foreach (var (line, quantity) in requested)
        {
            var ceiling = request.AdvanceBilling
                ? line.PendingInvoiceQuantity
                : line.DeliveredNotInvoicedQuantity;

            if (quantity > ceiling)
            {
                return Result.Failure<Guid>(Error.Validation("Quantity",
                    $"La ligne {line.LineNumber} « {line.ProductName} » n'a plus que {ceiling} à facturer " +
                    $"(demandé : {quantity})"));
            }
        }

        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Aucun contexte d'entreprise disponible.");

        var issueDate = request.IssueDate?.Date ?? DateTime.UtcNow.Date;

        var docResult = await _documentNumberService.ReserveNextAsync(
            tenantId, NumberingDocumentType.Invoice, issueDate.Year, issueDate, cancellationToken);

        var number = InvoiceNumber.Create(docResult.Prefix ?? "FAC", docResult.Year, docResult.Sequence);

        var invoiceResult = Invoice.CreateFromSalesOrder(
            number,
            order.Client,
            issueDate,
            order.Id,
            request.DueDate,
            reference: $"Commande {order.Number.Value}",
            notes: order.Notes,
            paymentTerms: order.PaymentTerms,
            warehouseId: order.WarehouseId);

        if (invoiceResult.IsFailure)
            return Result.Failure<Guid>(invoiceResult.Error);

        var invoice = invoiceResult.Value;

        foreach (var (line, quantity) in requested)
        {
            var product = line.Product
                ?? await _productRepository.GetByIdAsync(line.ProductId, cancellationToken);

            if (product is null)
                return Result.Failure<Guid>(Error.NotFound("Produit", line.ProductId));

            // Prix, remise et FODEC repris de la commande : la facture doit égaler
            // l'engagement, au millime.
            var addResult = invoice.AddLine(
                product,
                quantity,
                line.UnitPrice,
                line.DiscountPercent,
                line.FodecRatePercent);

            if (addResult.IsFailure)
                return Result.Failure<Guid>(addResult.Error);
        }

        var stamp = await _fiscalStampResolver.ResolveSignedStampAsync(isCreditNote: false, cancellationToken);
        var stampResult = invoice.SetFiscalStampAmount(stamp);
        if (stampResult.IsFailure)
            return Result.Failure<Guid>(stampResult.Error);

        invoice.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system");
        await _invoiceRepository.AddAsync(invoice, cancellationToken);

        await _auditService.LogAsync(
            AuditActions.Invoice.Created,
            "Invoice",
            invoice.Id,
            newValues: new
            {
                invoice.Number.Value,
                invoice.TotalAmount.Amount,
                SourceSalesOrderId = order.Id,
                SourceSalesOrderNumber = order.Number.Value,
                request.AdvanceBilling
            },
            cancellationToken: cancellationToken);

        return Result.Success(invoice.Id);
    }

    private static List<(SalesOrderLine Line, decimal Quantity)> ResolveRequestedLines(
        SalesOrder order,
        IReadOnlyList<SalesOrderInvoiceLineDto>? requested,
        bool advanceBilling)
    {
        decimal Available(SalesOrderLine l) =>
            advanceBilling ? l.PendingInvoiceQuantity : l.DeliveredNotInvoicedQuantity;

        if (requested is null || requested.Count == 0)
        {
            return order.Lines
                .Where(l => Available(l) > 0)
                .OrderBy(l => l.LineNumber)
                .Select(l => (l, Available(l)))
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
