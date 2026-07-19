using FactuTrust.Domain.Enums;

namespace FactuTrust.Application.Common.Interfaces;

/// <summary>
/// Provides access to the current authenticated user's information.
/// </summary>
public interface ICurrentUser
{
    /// <summary>
    /// Gets the current user's ID.
    /// </summary>
    Guid? UserId { get; }
    
    /// <summary>
    /// Gets the current user's email.
    /// </summary>
    string? Email { get; }
    
    /// <summary>
    /// Gets the current user's tenant ID.
    /// </summary>
    Guid? TenantId { get; }
    
    /// <summary>
    /// Gets the current user's role.
    /// </summary>
    UserRole? Role { get; }
    
    /// <summary>
    /// Gets whether the user is authenticated.
    /// </summary>
    bool IsAuthenticated { get; }
    
    /// <summary>
    /// Checks if the user has a specific permission.
    /// </summary>
    bool HasPermission(string permission);

    /// <summary>
    /// True when an accounting firm user is operating on a delegated client dossier.
    /// </summary>
    bool IsAccountingFirmDelegatedContext { get; }
    
    /// <summary>
    /// Gets the user's IP address.
    /// </summary>
    string? IpAddress { get; }
    
    /// <summary>
    /// Gets the user's user agent.
    /// </summary>
    string? UserAgent { get; }
}
