using FactuTrust.Domain.Common;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Représente une ligne de comptage d'inventaire pour un produit.
/// Chaque ligne contient la quantité théorique (système) et la quantité comptée (réelle).
/// </summary>
public sealed class InventoryCountLine : Entity
{
    public Guid InventoryId { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public string? ProductCode { get; private set; }
    
    /// <summary>
    /// Quantité théorique dans le système au moment du démarrage de l'inventaire.
    /// </summary>
    public decimal TheoreticalQuantity { get; private set; }
    
    /// <summary>
    /// Quantité réellement comptée par l'utilisateur.
    /// Null si pas encore compté.
    /// </summary>
    public decimal? CountedQuantity { get; private set; }
    
    /// <summary>
    /// Différence entre la quantité comptée et théorique.
    /// Positif = surplus trouvé, Négatif = manque.
    /// </summary>
    public decimal Difference => (CountedQuantity ?? 0) - TheoreticalQuantity;
    
    /// <summary>
    /// Indique si ce produit a été compté.
    /// </summary>
    public bool IsCounted => CountedQuantity.HasValue;
    
    /// <summary>
    /// Date/heure du comptage.
    /// </summary>
    public DateTime? CountedAt { get; private set; }

    private InventoryCountLine() { }

    internal static InventoryCountLine Create(
        Guid inventoryId,
        Guid productId,
        string productName,
        string? productCode,
        decimal theoreticalQuantity)
    {
        return new InventoryCountLine
        {
            InventoryId = inventoryId,
            ProductId = productId,
            ProductName = productName,
            ProductCode = productCode,
            TheoreticalQuantity = theoreticalQuantity,
            CountedQuantity = null,
            CountedAt = null
        };
    }

    /// <summary>
    /// Enregistre le comptage pour ce produit.
    /// </summary>
    internal Result RecordCount(decimal countedQuantity)
    {
        if (countedQuantity < 0)
            return Result.Failure(Error.Validation("CountedQuantity", "La quantité comptée ne peut pas être négative."));

        CountedQuantity = countedQuantity;
        CountedAt = DateTime.UtcNow;
        return Result.Success();
    }

    /// <summary>
    /// Génère un message pédagogique expliquant l'écart.
    /// </summary>
    public string GetHumanMessage()
    {
        if (!IsCounted)
            return "❓ Non compté";

        if (Difference == 0)
            return "✅ Stock exact";

        var absDiff = Math.Abs(Difference);
        var unitText = absDiff == 1 ? "unité" : "unités";

        if (Difference > 0)
            return $"➕ +{absDiff} {unitText} trouvée{(absDiff > 1 ? "s" : "")}";
        else
            return $"➖ -{absDiff} {unitText} manquante{(absDiff > 1 ? "s" : "")}";
    }
}
