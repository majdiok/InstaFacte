using System.Security.Claims;
using FactuTrust.Domain.Auth;
using FactuTrust.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace FactuTrust.API.Authorization;

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
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
            return Task.CompletedTask;
        }

        if (fromClaims.Count > 0)
        {
            if (fromClaims.Contains(requirement.Permission))
                context.Succeed(requirement);
            return Task.CompletedTask;
        }

        var roleClaim = user.FindFirst(ClaimTypes.Role)?.Value;
        if (Enum.TryParse<UserRole>(roleClaim, out var role))
        {
            if (role.GetPermissions().Contains(requirement.Permission))
                context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
