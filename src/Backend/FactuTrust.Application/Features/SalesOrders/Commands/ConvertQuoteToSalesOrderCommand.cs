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

/// <summary>
/// Transforme un devis accepté en commande client — le maillon qui manquait entre la
/// proposition commerciale et la livraison.
/// </summary>
public sealed record ConvertQuoteToSalesOrderCommand(
    Guid QuoteId,
    DateTime? OrderDate = null,
    DateTime? ExpectedDeliveryDate = null,
    Guid? WarehouseId = null) : IRequest<Result<Guid>>;

public sealed class ConvertQuoteToSalesOrderCommandHandler
    : IRequestHandler<ConvertQuoteToSalesOrderCommand, Result<Guid>>
{
    private readonly IQuoteRepository _quoteRepository;
    private readonly ISalesOrderRepository _salesOrderRepository;
    private readonly IProductRepository _productRepository;
    private readonly IDocumentNumberService _documentNumberService;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;

    public ConvertQuoteToSalesOrderCommandHandler(
        IQuoteRepository quoteRepository,
        ISalesOrderRepository salesOrderRepository,
        IProductRepository productRepository,
        IDocumentNumberService documentNumberService,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IAuditService auditService)
    {
        _quoteRepository = quoteRepository;
        _salesOrderRepository = salesOrderRepository;
        _productRepository = productRepository;
        _documentNumberService = documentNumberService;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<Result<Guid>> Handle(
        ConvertQuoteToSalesOrderCommand request, CancellationToken cancellationToken)
    {
        var quote = await _quoteRepository.GetByIdWithLinesAsync(request.QuoteId, cancellationToken);
        if (quote is null)
            return Result.Failure<Guid>(Error.NotFound("Devis", request.QuoteId));

        if (quote.ConvertedSalesOrderId.HasValue)
            return Result.Failure<Guid>(Error.Conflict("Ce devis a déjà été transformé en commande client"));

        if (quote.Status != QuoteStatus.Accepted)
            return Result.Failure<Guid>(Error.Validation("Status",
                "Seul un devis accepté peut être transformé en commande"));

        if (quote.Lines.Count == 0)
            return Result.Failure<Guid>(Error.Validation("Lines", "Ce devis ne contient aucune ligne"));

        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Aucun contexte d'entreprise disponible.");

        var orderDate = request.OrderDate?.Date ?? DateTime.UtcNow.Date;

        var docResult = await _documentNumberService.ReserveNextAsync(
            tenantId, NumberingDocumentType.SalesOrder, orderDate.Year, orderDate, cancellationToken);

        var number = SalesOrderNumber.Create(docResult.Prefix ?? "CDE", docResult.Year, docResult.Sequence);

        var orderResult = SalesOrder.Create(
            number,
            quote.Client,
            orderDate,
            request.ExpectedDeliveryDate,
            quote.Reference,
            quote.Notes,
            paymentTerms: null,
            warehouseId: request.WarehouseId,
            sourceQuoteId: quote.Id);

        if (orderResult.IsFailure)
            return Result.Failure<Guid>(orderResult.Error);

        var order = orderResult.Value;

        // Le devis est la source de vérité : prix, remise et taux FODEC sont repris tels
        // quels, pour que la commande porte exactement le montant accepté par le client.
        foreach (var line in quote.Lines.OrderBy(l => l.LineNumber))
        {
            if (!line.ProductId.HasValue)
            {
                return Result.Failure<Guid>(Error.Validation("Lines",
                    $"La ligne {line.LineNumber} « {line.ProductName} » est une ligne libre : " +
                    "une commande client exige un produit référencé pour suivre les livraisons"));
            }

            var product = line.Product
                ?? await _productRepository.GetByIdAsync(line.ProductId.Value, cancellationToken);

            if (product is null)
                return Result.Failure<Guid>(Error.NotFound("Produit", line.ProductId.Value));

            var addResult = order.AddLine(
                product,
                line.Quantity,
                line.UnitPrice,
                line.DiscountPercent,
                line.FodecRatePercent);

            if (addResult.IsFailure)
                return Result.Failure<Guid>(addResult.Error);
        }

        // Timbre repris du devis plutôt que re-résolu : la commande doit égaler ce que le
        // client a accepté, même si le catalogue de taxes a changé entre-temps.
        var stampResult = order.SetFiscalStampAmount(quote.FiscalStampAmount);
        if (stampResult.IsFailure)
            return Result.Failure<Guid>(stampResult.Error);

        order.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system");

        // Verrouillage du devis AVANT la persistance de la commande : même unité de travail,
        // donc atomicité garantie — impossible de créer la commande sans marquer le devis.
        quote.MarkAsConvertedToSalesOrder(order.Id);
        await _quoteRepository.UpdateAsync(quote, cancellationToken);

        await _salesOrderRepository.AddAsync(order, cancellationToken);

        await _auditService.LogAsync(
            AuditActions.SalesOrder.Created,
            "SalesOrder",
            order.Id,
            newValues: new
            {
                order.Number.Value,
                order.TotalAmount.Amount,
                SourceQuoteId = quote.Id,
                SourceQuoteNumber = quote.Number.Value
            },
            cancellationToken: cancellationToken);

        return Result.Success(order.Id);
    }
}
