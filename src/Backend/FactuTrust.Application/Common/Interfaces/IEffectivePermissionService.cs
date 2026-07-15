using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common.Interfaces;

/// <summary>
/// Resolves effective permissions and module visibility for a user (role ∩ module grants).
/// </summary>
public interface IEffectivePermissionService
{
    /// <summary>
    /// Effective permission strings and enabled modules for API / JWT / profile.
    /// </summary>
    Task<UserAccessSnapshot> GetUserAccessSnapshotAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <param name="EffectivePermissions">Final permission set after module filtering.</param>
/// <param name="EnabledModules">Modules for UI/JWT: all values when no grant rows; otherwise toggled-on modules that have ≥1 effective permission.</param>
/// <param name="IsModulePermissionScoped">True when <c>UserModuleGrants</c> rows exist; JWT must not fall back to role for <c>perm:</c> policies.</param>
public sealed record UserAccessSnapshot(
    IReadOnlySet<string> EffectivePermissions,
    IReadOnlyList<AppModule> EnabledModules,
    bool IsModulePermissionScoped);
