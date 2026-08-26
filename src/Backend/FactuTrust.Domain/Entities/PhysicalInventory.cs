using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.Events;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Représente un inventaire physique.
/// L'utilisateur compte ses produits réels et le système ajuste le stock.
/// </summary>
public sealed class PhysicalInventory : AggregateRoot
{
    private readonly List<InventoryCountLine> _countLines = new();

    /// <summary>
    /// Référence unique lisible (ex. INVE-000001).
    /// </summary>
    public string Reference { get; private set; } = string.Empty;

    public Guid WarehouseId { get; private set; }

    /// <summary>
    /// Navigation vers l'entrepôt (rempli par EF lors des requêtes avec Include).
    /// </summary>
    public Warehouse? Warehouse { get; private set; }

    public DateTime StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public InventoryType Type { get; private set; }
    public InventoryStatus Status { get; private set; }
    public string? Notes { get; private set; }
    
    /// <summary>
    /// Lignes de comptage pour chaque produit.
    /// </summary>
    public IReadOnlyCollection<InventoryCountLine> CountLines => _countLines.AsReadOnly();

    /// <summary>
    /// Nombre total de produits à compter.
    /// </summary>
    public int TotalProducts => _countLines.Count;

    /// <summary>
    /// Nombre de produits déjà comptés.
    /// </summary>
    public int CountedProducts => _countLines.Count(l => l.IsCounted);

    /// <summary>
    /// Nombre de produits avec écart après comptage.
    /// </summary>
    public int ProductsWithDifference => _countLines.Count(l => l.IsCounted && l.Difference != 0);

    /// <summary>
    /// Indique si tous les produits ont été comptés.
    /// </summary>
    public bool IsComplete => _countLines.All(l => l.IsCounted);

    private PhysicalInventory() { }

    /// <summary>
    /// Démarre un nouvel inventaire physique.
    /// </summary>
    /// <param name="reference">Référence unique (ex. INVE-000001).</param>
    public static Result<PhysicalInventory> Start(
        string reference,
        Guid warehouseId,
        InventoryType type,
        IEnumerable<(Guid ProductId, string ProductName, string? ProductCode, decimal TheoreticalQuantity)> products,
        string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return Result.Failure<PhysicalInventory>(Error.Validation("Reference", "La référence est obligatoire."));
        if (reference.Length > 50)
            return Result.Failure<PhysicalInventory>(Error.Validation("Reference", "La référence ne peut pas dépasser 50 caractères."));
        if (warehouseId == Guid.Empty)
            return Result.Failure<PhysicalInventory>(Error.Validation("WarehouseId", "L'entrepôt est obligatoire."));

        var productList = products.ToList();
        if (!productList.Any())
            return Result.Failure<PhysicalInventory>(Error.Validation("Products", "Aucun produit à inventorier."));

        var inventory = new PhysicalInventory
        {
            Reference = reference.Trim(),
            WarehouseId = warehouseId,
            StartedAt = DateTime.UtcNow,
            Type = type,
            Status = InventoryStatus.InProgress,
            Notes = notes?.Trim()
        };

        foreach (var product in productList)
        {
            var line = InventoryCountLine.Create(
                inventory.Id,
                product.ProductId,
                product.ProductName,
                product.ProductCode,
                product.TheoreticalQuantity);
            inventory._countLines.Add(line);
        }

        inventory.AddDomainEvent(new InventoryStartedEvent(
            inventory.Id,
            warehouseId,
            productList.Count));

        return Result.Success(inventory);
    }

    public static Result<PhysicalInventory> StartDetailed(
        string reference,
        Guid warehouseId,
        InventoryType type,
        IEnumerable<(Guid ProductId, string ProductName, string? ProductCode, decimal TheoreticalQuantity, Guid? ProductLotId, string? LotNumber)> products,
        string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(reference))
            return Result.Failure<PhysicalInventory>(Error.Validation("Reference", "La référence est obligatoire."));
        if (reference.Length > 50)
            return Result.Failure<PhysicalInventory>(Error.Validation("Reference", "La référence ne peut pas dépasser 50 caractères."));
        if (warehouseId == Guid.Empty)
            return Result.Failure<PhysicalInventory>(Error.Validation("WarehouseId", "L'entrepôt est obligatoire."));

        var productList = products.ToList();
        if (!productList.Any())
            return Result.Failure<PhysicalInventory>(Error.Validation("Products", "Aucun produit à inventorier."));

        var inventory = new PhysicalInventory
        {
            Reference = reference.Trim(),
            WarehouseId = warehouseId,
            StartedAt = DateTime.UtcNow,
            Type = type,
            Status = InventoryStatus.InProgress,
            Notes = notes?.Trim()
        };

        foreach (var product in productList)
        {
            var line = InventoryCountLine.Create(
                inventory.Id,
                product.ProductId,
                product.ProductName,
                product.ProductCode,
                product.TheoreticalQuantity,
                product.ProductLotId,
                product.LotNumber);
            inventory._countLines.Add(line);
        }

        inventory.AddDomainEvent(new InventoryStartedEvent(
            inventory.Id,
            warehouseId,
            productList.Count));

        return Result.Success(inventory);
    }

    /// <summary>
    /// Enregistre le comptage d'un produit.
    /// </summary>
    public Result RecordCount(Guid productId, decimal countedQuantity, Guid? productLotId = null, string? lotNumber = null)
    {
        if (Status != InventoryStatus.InProgress)
            return Result.Failure(Error.Validation("Status", "Cet inventaire n'est plus en cours."));

        var line = productLotId.HasValue
            ? _countLines.FirstOrDefault(l => l.ProductId == productId && l.ProductLotId == productLotId)
            : _countLines.FirstOrDefault(l => l.ProductId == productId && l.ProductLotId == null)
              ?? _countLines.FirstOrDefault(l => l.ProductId == productId);
        if (line == null)
            return Result.Failure(Error.NotFound("Produit", productId));

        return line.RecordCount(countedQuantity, lotNumber);
    }

    /// <summary>
    /// Confirme les lignes non saisies à la quantité théorique (aucun écart).
    /// </summary>
    public Result ConfirmUncountedAsTheoretical()
    {
        if (Status != InventoryStatus.InProgress)
            return Result.Failure(Error.Validation("Status", "Cet inventaire n'est plus en cours."));

        foreach (var line in _countLines.Where(l => !l.IsCounted))
        {
            var result = line.RecordCount(line.TheoreticalQuantity);
            if (result.IsFailure)
                return result;
        }

        return Result.Success();
    }

    /// <summary>
    /// Valide l'inventaire et génère les ajustements de stock.
    /// Les lignes non saisies sont confirmées à la quantité système.
    /// </summary>
    public Result Validate()
    {
        if (Status != InventoryStatus.InProgress)
            return Result.Failure(Error.Validation("Status", "Cet inventaire n'est plus en cours."));

        var confirmResult = ConfirmUncountedAsTheoretical();
        if (confirmResult.IsFailure)
            return confirmResult;

        Status = InventoryStatus.Validated;
        CompletedAt = DateTime.UtcNow;

        // Prepare adjustments for event
        var adjustments = _countLines
            .Where(l => l.Difference != 0)
            .Select(l => new InventoryAdjustmentItem(
                l.ProductId,
                l.ProductName,
                l.TheoreticalQuantity,
                l.CountedQuantity!.Value,
                l.Difference))
            .ToList();

        AddDomainEvent(new InventoryValidatedEvent(
            Id,
            WarehouseId,
            TotalProducts,
            ProductsWithDifference,
            adjustments));

        return Result.Success();
    }

    /// <summary>
    /// Annule l'inventaire sans impact sur le stock.
    /// </summary>
    public Result Cancel()
    {
        if (Status != InventoryStatus.InProgress)
            return Result.Failure(Error.Validation("Status", "Seul un inventaire en cours peut être annulé."));

        Status = InventoryStatus.Cancelled;
        CompletedAt = DateTime.UtcNow;

        AddDomainEvent(new InventoryCancelledEvent(Id));

        return Result.Success();
    }

    /// <summary>
    /// Génère un résumé pédagogique de l'inventaire.
    /// </summary>
    public InventorySummary GetSummary()
    {
        var productsOk = _countLines.Where(l => l.IsCounted && l.Difference == 0).ToList();
        var productsWithDiff = _countLines.Where(l => l.IsCounted && l.Difference != 0).ToList();
        var productsNotCounted = _countLines.Where(l => !l.IsCounted).ToList();

        return new InventorySummary(
            TotalProducts,
            CountedProducts,
            productsOk.Select(l => new InventorySummaryItem(l.ProductId, l.ProductName, l.ProductCode, l.GetHumanMessage())).ToList(),
            productsWithDiff.Select(l => new InventorySummaryItem(l.ProductId, l.ProductName, l.ProductCode, l.GetHumanMessage(), l.Difference)).ToList(),
            productsNotCounted.Select(l => new InventorySummaryItem(l.ProductId, l.ProductName, l.ProductCode, l.GetHumanMessage())).ToList());
    }
}

/// <summary>
/// Résumé pédagogique d'un inventaire.
/// </summary>
public sealed record InventorySummary(
    int TotalProducts,
    int CountedProducts,
    IReadOnlyList<InventorySummaryItem> ProductsOk,
    IReadOnlyList<InventorySummaryItem> ProductsWithDifference,
    IReadOnlyList<InventorySummaryItem> ProductsNotCounted);

/// <summary>
/// Élément du résumé d'inventaire.
/// </summary>
public sealed record InventorySummaryItem(
    Guid ProductId,
    string ProductName,
    string? ProductCode,
    string HumanMessage,
    decimal Difference = 0);
