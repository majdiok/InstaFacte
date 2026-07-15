namespace FactuTrust.Application.DTOs;

/// <summary>
/// DTO for product category.
/// </summary>
public sealed record ProductCategoryDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = null!;
    public string Name { get; init; } = null!;
    public int DisplayOrder { get; init; }
    public bool IsActive { get; init; }
}

/// <summary>
/// DTO for product category selection (dropdown).
/// </summary>
public sealed record ProductCategorySelectDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = null!;
}

/// <summary>
/// DTO for creating a product category.
/// </summary>
public sealed record CreateProductCategoryDto
{
    public string Code { get; init; } = null!;
    public string Name { get; init; } = null!;
    public int DisplayOrder { get; init; }
}

/// <summary>
/// DTO for updating a product category.
/// </summary>
public sealed record UpdateProductCategoryDto
{
    public string Name { get; init; } = null!;
    public int DisplayOrder { get; init; }
    public bool IsActive { get; init; }
}
