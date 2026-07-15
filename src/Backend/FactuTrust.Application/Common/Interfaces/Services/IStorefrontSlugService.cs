namespace FactuTrust.Application.Common.Interfaces.Services;

/// <summary>
/// Generates safe, unique URL slugs for storefronts (e.g. from a tenant's company name).
/// The slug must match <c>^[a-z0-9]([a-z0-9-]{1,58}[a-z0-9])?$</c> and be globally unique across
/// all tenants in the Master database.
/// </summary>
public interface IStorefrontSlugService
{
    /// <summary>
    /// Normalizes the proposed slug (lowercase, ASCII, trimmed) and checks availability.
    /// Returns a collision-free candidate by appending a numeric suffix if needed.
    /// </summary>
    /// <param name="proposed">Raw input (may contain accents, spaces, uppercase).</param>
    /// <param name="excludingStorefrontId">When updating an existing storefront, pass its id to ignore its own slug during uniqueness checks.</param>
    Task<string> GenerateUniqueAsync(string proposed, Guid? excludingStorefrontId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates that a slug is syntactically correct AND not a reserved word (api, admin, auth, etc.).
    /// </summary>
    bool IsValid(string slug, out string? errorMessage);
}
