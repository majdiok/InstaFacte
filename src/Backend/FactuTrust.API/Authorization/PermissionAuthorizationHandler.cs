using System.Security.Claims;
using FactuTrust.Domain.Auth;
using FactuTrust.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;

namespace FactuTrust.API.Authorization;

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly ILogger<PermissionAuthorizationHandler> _logger;

    public PermissionAuthorizationHandler(ILogger<PermissionAuthorizationHandler> logger)
    {
        _logger = logger;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var user = context.User;
        if (!user.Identity?.IsAuthenticated ?? true)
            return Task.CompletedTask;

        var moduleScoped = user.FindFirst(AuthClaimTypes.PermissionSource)?.Value == "modules";

        var fromClaims = user.FindAll(AuthClaimTypes.Permission).Select(c => c.Value).ToHashSet();
        if (moduleScoped)
        {
            if (fromClaims.Contains(requirement.Permission))
                context.Succeed(requirement);
            else
                LogDenied(user, requirement.Permission);
            return Task.CompletedTask;
        }

        if (fromClaims.Count > 0)
        {
            if (fromClaims.Contains(requirement.Permission))
                context.Succeed(requirement);
            else
                LogDenied(user, requirement.Permission);
            return Task.CompletedTask;
        }

        var roleClaim = user.FindFirst(ClaimTypes.Role)?.Value;
        if (Enum.TryParse<UserRole>(roleClaim, out var role) && role.GetPermissions().Contains(requirement.Permission))
        {
            context.Succeed(requirement);
        }
        else
        {
            LogDenied(user, requirement.Permission);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Security audit trail (plan §5.3/§7): userId + permission key only — no email/name/claims dump,
    /// so this never leaks PII into logs even on repeated/automated probing.
    /// </summary>
    private void LogDenied(ClaimsPrincipal user, string permission)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
        _logger.LogWarning(
            "PermissionAuthorizationHandler: permission denied (userId={UserId}, permission={Permission}).",
            userId,
            permission);
    }
}
