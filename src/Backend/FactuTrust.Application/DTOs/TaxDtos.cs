namespace FactuTrust.Application.DTOs;

/// <summary>
/// Read model for a configurable tax rule.
/// </summary>
public sealed record TaxDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
    public int Type { get; init; }
    public string TypeDisplay { get; init; } = null!;
    public int ValueType { get; init; }
    public string ValueTypeDisplay { get; init; } = null!;
    public decimal Value { get; init; }
    public int Context { get; init; }
    public string ContextDisplay { get; init; } = null!;
    public bool IsAppliedToProducts { get; init; }
    public bool IsActive { get; init; }
    public bool IsSystem { get; init; }
    public int DisplayOrder { get; init; }
}

/// <summary>
/// Create tax (user-defined or additional catalog entry).
/// </summary>
public sealed record CreateTaxDto
{
    public string Name { get; init; } = null!;
    public int Type { get; init; }
    public int ValueType { get; init; }
    public decimal Value { get; init; }
    public int Context { get; init; }
    public bool IsAppliedToProducts { get; init; }
    public int DisplayOrder { get; init; }
}

/// <summary>
/// Update tax (system taxes: limited fields applied in handler).
/// </summary>
public sealed record UpdateTaxDto
{
    public string Name { get; init; } = null!;
    public int Type { get; init; }
    public int ValueType { get; init; }
    public decimal Value { get; init; }
    public int Context { get; init; }
    public bool IsAppliedToProducts { get; init; }
    public int DisplayOrder { get; init; }
}

/// <summary>
/// VAT rate option for product/invoice dropdowns (active TVA rules only).
/// </summary>
public sealed record VatRateOptionDto
{
    public Guid Id { get; init; }
    public int Percent { get; init; }
    public string Label { get; init; } = null!;
}

/// <summary>
/// Activer ou désactiver une taxe.
/// </summary>
public sealed record SetTaxActiveDto
{
    public bool IsActive { get; init; }
}
