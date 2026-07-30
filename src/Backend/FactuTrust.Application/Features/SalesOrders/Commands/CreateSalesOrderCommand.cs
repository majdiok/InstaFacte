using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Pricing;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Services;
using FactuTrust.Domain.ValueObjects;
using FluentValidation;
using MediatR;

namespace FactuTrust.Application.Features.SalesOrders.Commands;

/// <summary>Crée une commande client en brouillon.</summary>
public sealed record CreateSalesOrderCommand(CreateSalesOrderDto Order) : IRequest<Result<Guid>>;

public sealed class CreateSalesOrderCommandValidator : AbstractValidator<CreateSalesOrderCommand>
{
    public CreateSalesOrderCommandValidator()
    {
        RuleFor(x => x.Order.ClientId).NotEmpty().WithMessage("Le client est obligatoire");
        RuleFor(x => x.Order.OrderDate).NotEmpty().WithMessage("La date de commande est obligatoire");
        RuleFor(x => x.Order.Lines).NotEmpty().WithMessage("La commande doit contenir au moins une ligne");

        RuleForEach(x => x.Order.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId).NotEmpty().WithMessage("Le produit est obligatoire");
            line.RuleFor(l => l.Quantity).GreaterThan(0).WithMessage("La quantité doit être supérieure à zéro");
            line.RuleFor(l => l.UnitPrice).GreaterThanOrEqualTo(0).WithMessage("Le prix unitaire doit être positif");
            line.RuleFor(l => l.DiscountPercent)
                .InclusiveBetween(0, 100)
                .When(l => l.DiscountPercent.HasValue)
                .WithMessage("La remise doit être comprise entre 0% et 100%");
        });
    }
}

public sealed class CreateSalesOrderCommandHandler : IRequestHandler<CreateSalesOrderCommand, Result<Guid>>
{
    private readonly ISalesOrderRepository _salesOrderRepository;
    private readonly IClientRepository _clientRepository;
    private readonly IProductRepository _productRepository;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IDocumentNumberService _documentNumberService;
    private readonly IFiscalStampResolver _fiscalStampResolver;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;
    private readonly IPriceResolver _priceResolver;
    private readonly IPromotionResolver _promotionResolver;

    public CreateSalesOrderCommandHandler(
        ISalesOrderRepository salesOrderRepository,
        IClientRepository clientRepository,
        IProductRepository productRepository,
        IWarehouseRepository warehouseRepository,
        IDocumentNumberService documentNumberService,
        IFiscalStampResolver fiscalStampResolver,
        ITenantContext tenantContext,
        ICurrentUser currentUser,
        IAuditService auditService,
        IPriceResolver priceResolver,
        IPromotionResolver promotionResolver)
    {
        _priceResolver = priceResolver;
        _promotionResolver = promotionResolver;
        _salesOrderRepository = salesOrderRepository;
        _clientRepository = clientRepository;
        _productRepository = productRepository;
        _warehouseRepository = warehouseRepository;
        _documentNumberService = documentNumberService;
        _fiscalStampResolver = fiscalStampResolver;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<Result<Guid>> Handle(CreateSalesOrderCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Order;

        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Aucun contexte d'entreprise disponible.");

        var client = await _clientRepository.GetByIdAsync(dto.ClientId, cancellationToken);
        if (client is null)
            return Result.Failure<Guid>(Error.NotFound("Client", dto.ClientId));

        if (!client.IsActive)
            return Result.Failure<Guid>(Error.Validation("Client", "Ce client est désactivé"));

        if (dto.WarehouseId is { } warehouseId)
        {
            var warehouse = await _warehouseRepository.GetByIdAsync(warehouseId, cancellationToken);
            if (warehouse is null)
                return Result.Failure<Guid>(Error.NotFound("Warehouse", warehouseId));
            if (!warehouse.IsActive)
                return Result.Failure<Guid>(Error.Validation("Warehouse", "Cet entrepôt est désactivé"));
        }

        // Numéro réservé de façon atomique par le service de numérotation documentaire.
        var docResult = await _documentNumberService.ReserveNextAsync(
            tenantId,
            NumberingDocumentType.SalesOrder,
            dto.OrderDate.Year,
            dto.OrderDate,
            cancellationToken);

        var number = SalesOrderNumber.Create(docResult.Prefix ?? "CDE", docResult.Year, docResult.Sequence);

        var orderResult = SalesOrder.Create(
            number,
            client,
            dto.OrderDate,
            dto.ExpectedDeliveryDate,
            dto.Reference,
            dto.Notes,
            dto.PaymentTerms,
            dto.WarehouseId,
            dto.SourceQuoteId);

        if (orderResult.IsFailure)
            return Result.Failure<Guid>(orderResult.Error);

        var order = orderResult.Value;

        foreach (var lineDto in dto.Lines)
        {
            var product = await _productRepository.GetByIdAsync(lineDto.ProductId, cancellationToken);
            if (product is null)
                return Result.Failure<Guid>(Error.NotFound("Produit", lineDto.ProductId));

            if (!product.IsActive)
                return Result.Failure<Guid>(Error.Validation("Produit", $"Le produit '{product.Name}' est désactivé"));

            // Plafond de remise du catalogue, contrôlé ici comme pour le devis.
            if (lineDto.DiscountPercent is { } discount
                && product.IsDiscountEnabled
                && product.MaxDiscountPercent.HasValue
                && discount > product.MaxDiscountPercent.Value)
            {
                return Result.Failure<Guid>(Error.Validation(
                    "DiscountPercent",
                    $"La remise ne peut pas dépasser {product.MaxDiscountPercent.Value}% pour le produit '{product.Name}'"));
            }

            // Prix forcé par le vendeur, sinon résolu par le point unique (prix négocié →
            // grille → catalogue). Le prix obtenu est gravé sur la ligne : la commande ne
            // bougera plus si une grille change ensuite.
            Money? unitPrice = lineDto.UnitPrice > 0 ? Money.Create(lineDto.UnitPrice) : null;
            if (unitPrice is null)
            {
                var priceResult = await _priceResolver.ResolveUnitPriceAsync(
                    dto.ClientId, product.Id, lineDto.Quantity, dto.OrderDate, cancellationToken);
                if (priceResult.IsFailure)
                    return Result.Failure<Guid>(priceResult.Error);

                unitPrice = priceResult.Value.UnitPriceHT;
            }

            // Promotion : appliquée APRÈS le prix, sous forme de remise de ligne. Elle ne
            // s'impose jamais à une remise saisie par le commercial — ce serait une surprise
            // silencieuse. La remise obtenue est figée : la fin de la promotion ne change plus
            // cette commande.
            var discountPercent = lineDto.DiscountPercent;
            if (discountPercent is null)
            {
                var promo = await _promotionResolver.ResolveAsync(
                    product.Id, product.CategoryId, dto.ClientId,
                    lineDto.Quantity, unitPrice, dto.OrderDate, cancellationToken);

                if (promo.IsSuccess && promo.Value is { } applied)
                    discountPercent = applied.DiscountPercent;
            }

            var addResult = order.AddLine(
                product,
                lineDto.Quantity,
                unitPrice,
                discountPercent,
                notes: lineDto.Notes);

            if (addResult.IsFailure)
                return Result.Failure<Guid>(addResult.Error);
        }

        // Timbre fiscal annoncé dès la commande, avec le même résolveur que le devis et la
        // facture : la chaîne documentaire doit afficher le même total de bout en bout.
        var stamp = await _fiscalStampResolver.ResolveSignedStampAsync(isCreditNote: false, cancellationToken);
        var stampResult = order.SetFiscalStampAmount(stamp);
        if (stampResult.IsFailure)
            return Result.Failure<Guid>(stampResult.Error);

        order.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system");

        await _salesOrderRepository.AddAsync(order, cancellationToken);

        await _auditService.LogAsync(
            AuditActions.SalesOrder.Created,
            "SalesOrder",
            order.Id,
            newValues: new { order.Number.Value, order.TotalAmount.Amount, order.ClientId },
            cancellationToken: cancellationToken);

        return Result.Success(order.Id);
    }
}
