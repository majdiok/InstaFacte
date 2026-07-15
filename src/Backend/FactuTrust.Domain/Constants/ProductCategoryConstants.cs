namespace FactuTrust.Domain.Constants;

/// <summary>
/// Constants for the default product category used in migrations and seeding.
/// </summary>
public static class ProductCategoryConstants
{
    /// <summary>
    /// Fixed Guid for the default "general" category. Used in migrations to ensure
    /// consistent reference across all tenant databases.
    /// </summary>
    public static readonly Guid DefaultCategoryId = new("11111111-1111-1111-1111-111111111111");
}
