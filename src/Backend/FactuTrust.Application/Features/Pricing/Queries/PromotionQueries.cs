using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Pricing.Queries;

public sealed class PromotionDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public Guid? ProductId { get; init; }
    public Guid? ProductCategoryId { get; init; }
    public Guid? ClientId { get; init; }
    public string DiscountType { get; init; } = string.Empty;
    public decimal? DiscountPercent { get; init; }
    public decimal? DiscountAmount { get; init; }
    public decimal MinQuantity { get; init; }
    public DateTime StartsOn { get; init; }
    public DateTime EndsOn { get; init; }
    public bool IsActive { get; init; }
    public int Priority { get; init; }

    /// <summary>Vrai si la promotion court aujourd'hui — active ET dans sa fenêtre.</summary>
    public bool IsRunningToday { get; init; }

    /// <summary>Portée lisible : « Tous les produits », « Produit ciblé », etc.</summary>
    public string ScopeLabel { get; init; } = string.Empty;
}

public sealed record GetPromotionsQuery : IRequest<Result<IReadOnlyList<PromotionDto>>>;

public sealed class GetPromotionsQueryHandler
    : IRequestHandler<GetPromotionsQuery, Result<IReadOnlyList<PromotionDto>>>
{
    private readonly IPromotionRepository _repository;

    public GetPromotionsQueryHandler(IPromotionRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IReadOnlyList<PromotionDto>>> Handle(
        GetPromotionsQuery request, CancellationToken cancellationToken)
    {
        var promotions = await _repository.GetAllOrderedAsync(cancellationToken);
        var today = DateTime.UtcNow.Date;

        var items = promotions.Select(p => new PromotionDto
        {
            Id = p.Id,
            Name = p.Name,
            ProductId = p.ProductId,
            ProductCategoryId = p.ProductCategoryId,
            ClientId = p.ClientId,
            DiscountType = p.DiscountType.ToString(),
            DiscountPercent = p.DiscountPercent,
            DiscountAmount = p.DiscountAmount?.Amount,
            MinQuantity = p.MinQuantity,
            StartsOn = p.StartsOn,
            EndsOn = p.EndsOn,
            IsActive = p.IsActive,
            Priority = p.Priority,
            IsRunningToday = p.IsRunningAt(today),
            ScopeLabel = BuildScopeLabel(p.ProductId, p.ProductCategoryId, p.ClientId)
        }).ToList();

        return Result.Success<IReadOnlyList<PromotionDto>>(items);
    }

    private static string BuildScopeLabel(Guid? productId, Guid? categoryId, Guid? clientId)
    {
        var parts = new List<string>();

        if (productId.HasValue) parts.Add("un produit");
        else if (categoryId.HasValue) parts.Add("une catégorie");
        else parts.Add("tous les produits");

        parts.Add(clientId.HasValue ? "un client" : "tous les clients");

        return string.Join(" · ", parts);
    }
}

// ─────────────────────── Conditions de règlement ───────────────────────

public sealed class PaymentTermTemplateDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public int DelayDays { get; init; }
    public string DueMode { get; init; } = string.Empty;
    public int? DueDayOfMonth { get; init; }
    public decimal? EarlyPaymentDiscountPercent { get; init; }
    public int? EarlyPaymentDays { get; init; }
    public bool IsActive { get; init; }
    public bool IsDefault { get; init; }

    /// <summary>Libellé tel qu'il s'imprimera sur le document.</summary>
    public string DocumentLabel { get; init; } = string.Empty;

    /// <summary>Échéance qu'aurait un document émis aujourd'hui — rend la règle tangible.</summary>
    public DateTime SampleDueDate { get; init; }
}

public sealed record GetPaymentTermTemplatesQuery(bool ActiveOnly = false)
    : IRequest<Result<IReadOnlyList<PaymentTermTemplateDto>>>;

public sealed class GetPaymentTermTemplatesQueryHandler
    : IRequestHandler<GetPaymentTermTemplatesQuery, Result<IReadOnlyList<PaymentTermTemplateDto>>>
{
    private readonly IPaymentTermTemplateRepository _repository;

    public GetPaymentTermTemplatesQueryHandler(IPaymentTermTemplateRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IReadOnlyList<PaymentTermTemplateDto>>> Handle(
        GetPaymentTermTemplatesQuery request, CancellationToken cancellationToken)
    {
        var templates = request.ActiveOnly
            ? await _repository.GetActiveAsync(cancellationToken)
            : await _repository.GetAllAsync(cancellationToken);

        var today = DateTime.UtcNow.Date;

        var items = templates.Select(t => new PaymentTermTemplateDto
        {
            Id = t.Id,
            Name = t.Name,
            DelayDays = t.DelayDays,
            DueMode = t.DueMode.ToString(),
            DueDayOfMonth = t.DueDayOfMonth,
            EarlyPaymentDiscountPercent = t.EarlyPaymentDiscountPercent,
            EarlyPaymentDays = t.EarlyPaymentDays,
            IsActive = t.IsActive,
            IsDefault = t.IsDefault,
            DocumentLabel = t.ToDocumentLabel(),
            SampleDueDate = t.ComputeDueDate(today)
        }).ToList();

        return Result.Success<IReadOnlyList<PaymentTermTemplateDto>>(items);
    }
}
