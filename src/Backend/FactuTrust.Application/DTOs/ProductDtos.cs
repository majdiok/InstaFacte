using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.DTOs;

/// <summary>
/// DTO for product list.
/// </summary>
public sealed record ProductListDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = null!;
    public string Name { get; init; } = null!;
    public string Type { get; init; } = null!;
    public decimal UnitPrice { get; init; }
    public decimal? PurchasePrice { get; init; }
    public decimal? LastPurchasePrice { get; init; }
    public decimal? WeightedAverageCost { get; init; }
    public decimal? ProfitMarginPercent { get; init; }
    public decimal SalePriceTtc { get; init; }
    public string Currency { get; init; } = null!;
    public int VatRatePercent { get; init; }
    public string VatRateDisplay { get; init; } = null!;
    public string? Unit { get; init; }
    public bool IsActive { get; init; }
    public bool IsStockManaged { get; init; }
    public bool IsFodecApplicable { get; init; }
    public bool IsDiscountEnabled { get; init; }
    public decimal? MaxDiscountPercent { get; init; }
    public Guid CategoryId { get; init; }
    public string CategoryName { get; init; } = "general";
    public string? ImageUrl { get; init; }

    /// <summary>
    /// Quantité disponible (on hand − réservé) à l'entrepôt par défaut ; null si gestion de stock désactivée ou entrepôt indisponible.
    /// </summary>
    public decimal? QuantityAvailable { get; init; }
}

/// <summary>
/// DTO for product details.
/// </summary>
public sealed record ProductDetailDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = null!;
    public string Name { get; init; } = null!;
    public string? Description { get; init; }
    public ProductType Type { get; init; }
    public string TypeDisplay { get; init; } = null!;
    public decimal UnitPrice { get; init; }
    public decimal? PurchasePrice { get; init; }
    public decimal? LastPurchasePrice { get; init; }
    public decimal? WeightedAverageCost { get; init; }
    public decimal? ProfitMarginPercent { get; init; }
    public decimal SalePriceTtc { get; init; }
    public string Currency { get; init; } = null!;
    public VatRate VatRate { get; init; }
    public int VatRatePercent { get; init; }
    public string VatRateDisplay { get; init; } = null!;
    public string? Unit { get; init; }
    public bool IsActive { get; init; }
    public bool IsStockManaged { get; init; }
    public bool IsFodecApplicable { get; init; }
    public bool IsDiscountEnabled { get; init; }
    public decimal? MaxDiscountPercent { get; init; }
    public Guid CategoryId { get; init; }
    public string CategoryName { get; init; } = "general";
    public string? ImageUrl { get; init; }

    /// <summary>
    /// Fournisseur préféré (réapprovisionnement). Null = aucun ; l'acheteur doit choisir manuellement.
    /// Utilisé par le module Prévisions IA pour pré-rattacher les recommandations à un fournisseur.
    /// </summary>
    public Guid? PreferredSupplierId { get; init; }

    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

/// <summary>
/// DTO for product selection (dropdown).
/// </summary>
public sealed record ProductSelectDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = null!;
    public string Name { get; init; } = null!;
    public decimal UnitPrice { get; init; }
    public decimal? PurchasePrice { get; init; }
    public int VatRatePercent { get; init; }
    public string? Unit { get; init; }
    public bool IsFodecApplicable { get; init; }
    public bool IsDiscountEnabled { get; init; }
    public decimal? MaxDiscountPercent { get; init; }
}

/// <summary>
/// DTO for creating a product.
/// </summary>
public sealed record CreateProductDto
{
    public string Code { get; init; } = null!;
    public string Name { get; init; } = null!;
    public string? Description { get; init; }
    public ProductType Type { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal? PurchasePrice { get; init; }
    public decimal? ProfitMarginPercent { get; init; }
    public VatRate VatRate { get; init; }
    public string? Unit { get; init; }
    public bool? IsStockManaged { get; init; }
    public Guid? CategoryId { get; init; }

    /// <summary>Fournisseur préféré (optionnel) ; null = aucun.</summary>
    public Guid? PreferredSupplierId { get; init; }

    /// <summary>FODEC applicable (1% sur le HT) sur les ventes.</summary>
    public bool IsFodecApplicable { get; init; }

    /// <summary>Active un plafond de remise sur les lignes de vente.</summary>
    public bool IsDiscountEnabled { get; init; }

    /// <summary>Remise maximale (%) autorisée sur les lignes de vente.</summary>
    public decimal? MaxDiscountPercent { get; init; }
}

/// <summary>
/// DTO for updating a product.
/// </summary>
public sealed record UpdateProductDto
{
    public string Name { get; init; } = null!;
    public string? Description { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal? PurchasePrice { get; init; }
    public decimal? ProfitMarginPercent { get; init; }
    public VatRate VatRate { get; init; }
    public string? Unit { get; init; }
    public bool? IsStockManaged { get; init; }
    public Guid? CategoryId { get; init; }

    /// <summary>Fournisseur préféré (optionnel) ; null = aucun (efface la préférence).</summary>
    public Guid? PreferredSupplierId { get; init; }

    /// <summary>FODEC applicable (1% sur le HT) sur les ventes.</summary>
    public bool IsFodecApplicable { get; init; }

    /// <summary>Active un plafond de remise sur les lignes de vente.</summary>
    public bool IsDiscountEnabled { get; init; }

    /// <summary>Remise maximale (%) autorisée sur les lignes de vente.</summary>
    public decimal? MaxDiscountPercent { get; init; }
}
