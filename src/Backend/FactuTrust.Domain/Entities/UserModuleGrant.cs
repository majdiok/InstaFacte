using FactuTrust.Domain.Enums;

namespace FactuTrust.Domain.Entities;

/// <summary>
/// Per-user module toggle for access control (master database).
/// </summary>
public sealed class UserModuleGrant
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public AppModule Module { get; set; }
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// JSON array of feature keys (see <c>ModuleFeatureCatalog</c>).
    /// <c>null</c> = all sub-features of the module; <c>[]</c> = explicit selection of none.
    /// </summary>
    public string? EnabledFeatureKeys { get; set; }
}
