using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Pricing.Queries;

// ───────────────────────────────── DTOs ─────────────────────────────────

public sealed class PriceListListItemDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public DateTime? ValidFrom { get; init; }
    public DateTime? ValidUntil { get; init; }
    public int ItemCount { get; init; }

    /// <summary>
    /// Vrai si la grille peut servir un prix aujourd'hui. Une grille active mais hors période
    /// n'alimente rien : le distinguer évite de longues recherches de « prix qui ne s'applique pas ».
    /// </summary>
    public bool IsApplicableToday { get; init; }
}

/// <summary>Palier quantitatif : « à partir de MinQuantity, le prix devient UnitPriceHT ».</summary>
public sealed class PriceListTierDto
{
    public decimal MinQuantity { get; init; }
    public decimal UnitPriceHT { get; init; }
}

public sealed class PriceListItemDto
{
    public Guid ProductId { get; init; }
    public string ProductCode { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;

    /// <summary>Prix de base, appliqué en deçà du premier palier.</summary>
    public decimal UnitPriceHT { get; init; }
    public string Currency { get; init; } = string.Empty;

    /// <summary>Prix catalogue, pour montrer l'écart que la grille introduit.</summary>
    public decimal CatalogUnitPriceHT { get; init; }

    /// <summary>Paliers dégressifs, du seuil le plus bas au plus haut.</summary>
    public List<PriceListTierDto> Tiers { get; init; } = new();
}

public sealed class PriceListDetailDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public DateTime? ValidFrom { get; init; }
    public DateTime? ValidUntil { get; init; }
    public bool IsApplicableToday { get; init; }
    public int AssignedClientCount { get; init; }
    public List<PriceListItemDto> Items { get; init; } = new();
}

// ───────────────────────────────── Liste ─────────────────────────────────

public sealed record GetPriceListsQuery : IRequest<Result<IReadOnlyList<PriceListListItemDto>>>;

public sealed class GetPriceListsQueryHandler
    : IRequestHandler<GetPriceListsQuery, Result<IReadOnlyList<PriceListListItemDto>>>
{
    private readonly IPriceListRepository _repository;

    public GetPriceListsQueryHandler(IPriceListRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IReadOnlyList<PriceListListItemDto>>> Handle(
        GetPriceListsQuery request, CancellationToken cancellationToken)
    {
        var priceLists = await _repository.GetAllSummaryAsync(cancellationToken);
        var itemCounts = await _repository.GetItemCountsAsync(cancellationToken);
        var today = DateTime.UtcNow.Date;

        var items = priceLists
            .Select(p => new PriceListListItemDto
            {
                Id = p.Id,
                Name = p.Name,
                Currency = p.Currency,
                IsActive = p.IsActive,
                ValidFrom = p.ValidFrom,
                ValidUntil = p.ValidUntil,
                ItemCount = itemCounts.TryGetValue(p.Id, out var count) ? count : 0,
                IsApplicableToday = p.IsApplicableAt(today)
            })
            .ToList();

        return Result.Success<IReadOnlyList<PriceListListItemDto>>(items);
    }
}

// ───────────────────────────────── Détail ─────────────────────────────────

public sealed record GetPriceListByIdQuery(Guid Id) : IRequest<Result<PriceListDetailDto>>;

public sealed class GetPriceListByIdQueryHandler
    : IRequestHandler<GetPriceListByIdQuery, Result<PriceListDetailDto>>
{
    private readonly IPriceListRepository _repository;
    private readonly IProductRepository _productRepository;

    public GetPriceListByIdQueryHandler(
        IPriceListRepository repository, IProductRepository productRepository)
    {
        _repository = repository;
        _productRepository = productRepository;
    }

    public async Task<Result<PriceListDetailDto>> Handle(
        GetPriceListByIdQuery request, CancellationToken cancellationToken)
    {
        var priceList = await _repository.GetByIdWithItemsAsync(request.Id, cancellationToken);
        if (priceList is null)
            return Result.Failure<PriceListDetailDto>(Error.NotFound("Grille tarifaire", request.Id));

        var assignedCount = await _repository.CountAssignedClientsAsync(request.Id, cancellationToken);
        var today = DateTime.UtcNow.Date;

        var lines = new List<PriceListItemDto>(priceList.Items.Count);
        foreach (var item in priceList.Items)
        {
            var product = await _productRepository.GetByIdAsync(item.ProductId, cancellationToken);

            lines.Add(new PriceListItemDto
            {
                ProductId = item.ProductId,
                ProductCode = product?.Code ?? string.Empty,
                ProductName = product?.Name ?? "(produit supprimé)",
                UnitPriceHT = item.UnitPriceHT.Amount,
                Currency = item.UnitPriceHT.Currency,
                CatalogUnitPriceHT = product?.UnitPrice.Amount ?? 0m,
                Tiers = item.Tiers
                    .OrderBy(t => t.MinQuantity)
                    .Select(t => new PriceListTierDto
                    {
                        MinQuantity = t.MinQuantity,
                        UnitPriceHT = t.UnitPriceHT.Amount
                    })
                    .ToList()
            });
        }

        return Result.Success(new PriceListDetailDto
        {
            Id = priceList.Id,
            Name = priceList.Name,
            Currency = priceList.Currency,
            IsActive = priceList.IsActive,
            ValidFrom = priceList.ValidFrom,
            ValidUntil = priceList.ValidUntil,
            IsApplicableToday = priceList.IsApplicableAt(today),
            AssignedClientCount = assignedCount,
            Items = lines.OrderBy(l => l.ProductCode).ToList()
        });
    }
}

// ─────────────────── Prix négociés d'un client ───────────────────

public sealed class ClientProductPriceDto
{
    public Guid Id { get; init; }
    public Guid ProductId { get; init; }
    public string ProductCode { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public decimal UnitPriceHT { get; init; }
    public string Currency { get; init; } = string.Empty;
    public decimal CatalogUnitPriceHT { get; init; }
    public bool IsActive { get; init; }
    public DateTime? ValidFrom { get; init; }
    public DateTime? ValidUntil { get; init; }
    public bool IsApplicableToday { get; init; }
}

public sealed record GetClientPricingQuery(Guid ClientId)
    : IRequest<Result<ClientPricingDto>>;

/// <summary>Tarification propre à un client : sa grille affectée et ses prix négociés.</summary>
public sealed class ClientPricingDto
{
    public Guid ClientId { get; init; }
    public Guid? PriceListId { get; init; }
    public string? PriceListName { get; init; }
    public List<ClientProductPriceDto> NegotiatedPrices { get; init; } = new();
}

public sealed class GetClientPricingQueryHandler
    : IRequestHandler<GetClientPricingQuery, Result<ClientPricingDto>>
{
    private readonly IClientRepository _clientRepository;
    private readonly IClientProductPriceRepository _priceRepository;
    private readonly IPriceListRepository _priceListRepository;
    private readonly IProductRepository _productRepository;

    public GetClientPricingQueryHandler(
        IClientRepository clientRepository,
        IClientProductPriceRepository priceRepository,
        IPriceListRepository priceListRepository,
        IProductRepository productRepository)
    {
        _clientRepository = clientRepository;
        _priceRepository = priceRepository;
        _priceListRepository = priceListRepository;
        _productRepository = productRepository;
    }

    public async Task<Result<ClientPricingDto>> Handle(
        GetClientPricingQuery request, CancellationToken cancellationToken)
    {
        var client = await _clientRepository.GetByIdAsync(request.ClientId, cancellationToken);
        if (client is null)
            return Result.Failure<ClientPricingDto>(Error.NotFound("Client", request.ClientId));

        string? priceListName = null;
        if (client.PriceListId is { } priceListId)
        {
            var priceList = await _priceListRepository.GetByIdAsync(priceListId, cancellationToken);
            priceListName = priceList?.Name;
        }

        var prices = await _priceRepository.GetByClientAsync(request.ClientId, cancellationToken);
        var today = DateTime.UtcNow.Date;

        var negotiated = new List<ClientProductPriceDto>(prices.Count);
        foreach (var price in prices)
        {
            var product = await _productRepository.GetByIdAsync(price.ProductId, cancellationToken);

            negotiated.Add(new ClientProductPriceDto
            {
                Id = price.Id,
                ProductId = price.ProductId,
                ProductCode = product?.Code ?? string.Empty,
                ProductName = product?.Name ?? "(produit supprimé)",
                UnitPriceHT = price.UnitPriceHT.Amount,
                Currency = price.UnitPriceHT.Currency,
                CatalogUnitPriceHT = product?.UnitPrice.Amount ?? 0m,
                IsActive = price.IsActive,
                ValidFrom = price.ValidFrom,
                ValidUntil = price.ValidUntil,
                IsApplicableToday = price.IsApplicableAt(today)
            });
        }

        return Result.Success(new ClientPricingDto
        {
            ClientId = client.Id,
            PriceListId = client.PriceListId,
            PriceListName = priceListName,
            NegotiatedPrices = negotiated.OrderBy(p => p.ProductCode).ToList()
        });
    }
}
