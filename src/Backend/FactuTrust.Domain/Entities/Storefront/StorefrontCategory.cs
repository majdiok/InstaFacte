namespace FactuTrust.Domain.Entities.Storefront;

/// <summary>
/// Public-facing category for a storefront. Used to cluster shops in the 3D street
/// and to power category filters on the public browsing experience.
/// </summary>
public enum StorefrontCategory
{
    Retail = 0,
    Food = 1,
    Fashion = 2,
    Books = 3,
    Electronics = 4,
    Services = 5,
    Crafts = 6,
    Other = 99
}

public static class StorefrontCategoryExtensions
{
    public static string ToDisplayString(this StorefrontCategory category) => category switch
    {
        StorefrontCategory.Retail => "Commerce général",
        StorefrontCategory.Food => "Alimentation",
        StorefrontCategory.Fashion => "Mode",
        StorefrontCategory.Books => "Livres & Culture",
        StorefrontCategory.Electronics => "Électronique",
        StorefrontCategory.Services => "Services",
        StorefrontCategory.Crafts => "Artisanat",
        StorefrontCategory.Other => "Autre",
        _ => throw new ArgumentOutOfRangeException(nameof(category))
    };
}
