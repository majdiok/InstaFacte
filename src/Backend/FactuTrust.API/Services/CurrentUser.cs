using System.Security.Claims;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Auth;
using FactuTrust.Domain.Enums;

namespace FactuTrust.API.Services;

/// <summary>
/// Provides access to the current authenticated user's information.
/// </summary>
public sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            var claim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(claim, out var id) ? id : null;
        }
    }

    public string? Email => User?.FindFirst(ClaimTypes.Email)?.Value;

    public Guid? TenantId
    {
        get
        {
            var claim = User?.FindFirst("tenant_id")?.Value;
            return Guid.TryParse(claim, out var id) ? id : null;
        }
    }

    public UserRole? Role
    {
        get
        {
            var claim = User?.FindFirst(ClaimTypes.Role)?.Value;
            return Enum.TryParse<UserRole>(claim, out var role) ? role : null;
        }
    }

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public bool HasPermission(string permission)
    {
        if (!IsAuthenticated || Role is null)
            return false;

        var fromClaims = User?.FindAll(AuthClaimTypes.Permission).Select(c => c.Value).ToHashSet();
        if (fromClaims is { Count: > 0 })
            return fromClaims.Contains(permission);

        var permissions = Role.Value.GetPermissions();
        return permissions.Contains(permission);
    }

    public string? IpAddress
    {
        get
        {
            var context = _httpContextAccessor.HttpContext;
            var forwardedFor = context?.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            
            if (!string.IsNullOrEmpty(forwardedFor))
                return forwardedFor.Split(',').First().Trim();

            return context?.Connection.RemoteIpAddress?.ToString();
        }
    }

    public string? UserAgent => _httpContextAccessor.HttpContext?.Request.Headers["User-Agent"].FirstOrDefault();
}
