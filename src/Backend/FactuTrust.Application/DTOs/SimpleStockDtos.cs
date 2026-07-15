namespace FactuTrust.Application.DTOs;

/// <summary>
/// DTO simplifié pour affichage stock - langage humain non technique.
/// </summary>
public record SimpleStockDto(
    Guid ProductId,
    string ProductName,
    string ProductCode,
    string? ImageUrl,
    int QuantityAvailable,
    StockStatus Status,
    string StatusLabel,
    string StatusIcon,
    int? MinimumThreshold,
    string? AlertMessage);

/// <summary>
/// Statut stock en langage humain.
/// </summary>
public enum StockStatus
{
    /// <summary>🟢 En stock</summary>
    InStock,
    /// <summary>🟠 Presque fini</summary>
    RunningLow,
    /// <summary>🔴 Rupture</summary>
    OutOfStock
}

/// <summary>
/// Helper pour générer les messages pédagogiques.
/// </summary>
public static class StockStatusHelper
{
    public static (StockStatus Status, string Label, string Icon) GetStatus(decimal quantity, decimal minimumThreshold)
    {
        if (quantity <= 0)
            return (StockStatus.OutOfStock, "Rupture", "🔴");
        
        if (quantity <= minimumThreshold)
            return (StockStatus.RunningLow, "Presque fini", "🟠");
        
        return (StockStatus.InStock, "En stock", "🟢");
    }

    public static string? GetAlertMessage(string productName, decimal quantity, decimal minimumThreshold)
    {
        if (quantity <= 0)
            return $"⛔ {productName} est en rupture de stock.";
        
        if (quantity <= minimumThreshold)
            return $"⚠️ {productName} arrive à sa fin. Il ne reste que {(int)quantity} unités.";
        
        return null;
    }
}

/// <summary>
/// Vue d'ensemble simplifiée du stock.
/// </summary>
public record SimpleStockOverviewDto(
    int TotalProducts,
    int ProductsInStock,
    int ProductsRunningLow,
    int ProductsOutOfStock,
    List<SimpleStockDto> Items,
    List<StockAlertDto> Alerts);

/// <summary>
/// Alerte stock actionnable.
/// </summary>
public record StockAlertDto(
    Guid ProductId,
    string ProductName,
    string Message,
    string Severity,
    string ActionLabel,
    string ActionRoute);

/// <summary>
/// Résultat du comptage rapide.
/// </summary>
public record QuickInventoryResultDto(
    bool Success,
    string Message,
    int PreviousQuantity,
    int NewQuantity,
    int Difference);

/// <summary>
/// Requête de comptage rapide.
/// </summary>
public record QuickInventoryRequest(
    Guid ProductId,
    int ActualQuantity);

/// <summary>
/// Vérification disponibilité stock avant facture.
/// </summary>
public record StockAvailabilityCheckDto(
    bool CanProceed,
    string SummaryMessage,
    List<StockLineCheckDto> LineChecks);

/// <summary>
/// Vérification par ligne de facture.
/// </summary>
public record StockLineCheckDto(
    Guid ProductId,
    string ProductName,
    int RequestedQuantity,
    int AvailableQuantity,
    bool IsAvailable,
    string Message);
