using System;

namespace FactuTrust.Application.DTOs;

/// <summary>
/// Row representing cost information from stock exit movements, grouped by stock movement reference.
/// For exits, <see cref="ExitQuantity"/> is normalized as a positive quantity.
/// </summary>
public sealed record StockExitMovementCostRowDto
{
    public string Reference { get; init; } = null!;
    public Guid ProductId { get; init; }
    public decimal ExitQuantity { get; init; }
    public decimal UnitCost { get; init; }
}

