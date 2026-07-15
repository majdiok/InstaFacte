namespace FactuTrust.Domain.Entities.Storefront;

/// <summary>
/// Facade theme selected by the tenant for its 3D storefront. Maps to a GLTF
/// template in the public frontend. Intentionally a closed set so the Master
/// projection can be cached and referenced by asset keys.
/// </summary>
public enum FacadeTheme
{
    Classic = 0,
    Modern = 1,
    Vintage = 2,
    Minimal = 3,
    Artisan = 4
}

public static class FacadeThemeExtensions
{
    public static string ToDisplayString(this FacadeTheme theme) => theme switch
    {
        FacadeTheme.Classic => "Classique",
        FacadeTheme.Modern => "Moderne",
        FacadeTheme.Vintage => "Rétro",
        FacadeTheme.Minimal => "Minimaliste",
        FacadeTheme.Artisan => "Artisanal",
        _ => throw new ArgumentOutOfRangeException(nameof(theme))
    };
}
