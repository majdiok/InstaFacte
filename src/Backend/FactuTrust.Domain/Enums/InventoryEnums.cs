namespace FactuTrust.Domain.Enums;

/// <summary>
/// Type d'inventaire physique.
/// </summary>
public enum InventoryType
{
    /// <summary>
    /// Inventaire complet - tous les produits.
    /// </summary>
    Complete = 1,

    /// <summary>
    /// Inventaire partiel - produits sélectionnés.
    /// </summary>
    Partial = 2
}

/// <summary>
/// Statut d'un inventaire physique.
/// </summary>
public enum InventoryStatus
{
    /// <summary>
    /// En cours de comptage.
    /// </summary>
    InProgress = 1,

    /// <summary>
    /// Validé et appliqué au stock.
    /// </summary>
    Validated = 2,

    /// <summary>
    /// Annulé sans impact sur le stock.
    /// </summary>
    Cancelled = 3
}

/// <summary>
/// Extensions d'affichage pour les enums d'inventaire.
/// </summary>
public static class InventoryEnumExtensions
{
    public static string ToDisplayString(this InventoryStatus status) => status switch
    {
        InventoryStatus.InProgress => "En cours",
        InventoryStatus.Validated => "Valide",
        InventoryStatus.Cancelled => "Annulé",
        _ => status.ToString()
    };

    public static string ToCssClass(this InventoryStatus status) => status switch
    {
        InventoryStatus.InProgress => "status-in-progress",
        InventoryStatus.Validated => "status-validated",
        InventoryStatus.Cancelled => "status-cancelled",
        _ => "status-unknown"
    };

    public static string ToDisplayString(this InventoryType type) => type switch
    {
        InventoryType.Complete => "Complet",
        InventoryType.Partial => "Partiel",
        _ => type.ToString()
    };
}
