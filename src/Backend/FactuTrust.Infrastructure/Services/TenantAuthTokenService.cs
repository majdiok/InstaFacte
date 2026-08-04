using System.Diagnostics;
using System.Security.Claims;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Auth;
using FactuTrust.Domain.Authorization;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;

namespace FactuTrust.Infrastructure.Services;

public sealed class TenantAuthTokenService : ITenantAuthTokenService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEffectivePermissionService _effectivePermissionService;
    private readonly MasterDbContext _masterContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TenantAuthTokenService> _logger;

    public TenantAuthTokenService(
        UserManager<ApplicationUser> userManager,
        IEffectivePermissionService effectivePermissionService,
        MasterDbContext masterContext,
        IConfiguration configuration,
        ILogger<TenantAuthTokenService> logger)
    {
        _userManager = userManager;
        _effectivePermissionService = effectivePermissionService;
        _masterContext = masterContext;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<AuthResponseDto> GenerateTokensAsync(
        Guid userId,
        Guid homeTenantId,
        Guid? contextTenantId = null,
        string? contextCompanyName = null,
        CancellationToken cancellationToken = default)
    {
        var totalSw = Stopwatch.StartNew();
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException("User not found.");

        var homeTenant = await _masterContext.Tenants.FindAsync([homeTenantId], cancellationToken)
            ?? throw new InvalidOperationException("Tenant not found.");

        if (user.TenantId == Guid.Empty)
            throw new InvalidOperationException("Cannot issue token: user has no tenant.");

        var roles = await _userManager.GetRolesAsync(user);
        var roleName = roles.FirstOrDefault(r => !string.Equals(r, PlatformRoles.PlatformAdmin, StringComparison.Ordinal))
            ?? UserRole.Accountant.ToString();
        var roleEnum = Enum.TryParse<UserRole>(roleName, out var r) ? r : UserRole.Accountant;

        var isDelegated = contextTenantId.HasValue && contextTenantId.Value != Guid.Empty
            && homeTenant.Kind == TenantKind.AccountingFirm;

        IReadOnlyList<string> effectivePermissions;
        IReadOnlyList<int> enabledModuleIds;
        UserAccessSnapshot? nativeSnapshot = null;

        var isFirmManaged = false;
        if (isDelegated)
        {
            var contextTenant = await _masterContext.Tenants.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == contextTenantId!.Value, cancellationToken);
            // Only dossiers managed by *this* firm (cabinet-created, no platform account).
            isFirmManaged = contextTenant?.ManagedByFirmTenantId == homeTenantId;

            effectivePermissions = DelegatedPermissionCatalog.GetDelegatedPermissions(roleEnum).ToList();
            enabledModuleIds = isFirmManaged
                ? new[]
                {
                    (int)AppModule.Accounting,
                    (int)AppModule.Fiscal,
                    (int)AppModule.Reports,
                    (int)AppModule.Payroll
                }
                : new[]
                {
                    (int)AppModule.Accounting,
                    (int)AppModule.Fiscal,
                    (int)AppModule.Sales,
                    (int)AppModule.Treasury,
                    (int)AppModule.Reports,
                    (int)AppModule.Purchases,
                    (int)AppModule.Payroll
                };
        }
        else if (homeTenant.Kind == TenantKind.AccountingFirm)
        {
            nativeSnapshot = await _effectivePermissionService.GetUserAccessSnapshotAsync(user.Id, cancellationToken);
            effectivePermissions = nativeSnapshot.EffectivePermissions.ToList();
            enabledModuleIds = new List<int>
            {
                (int)AppModule.Administration,
                (int)AppModule.Honoraires
            };
        }
        else
        {
            nativeSnapshot = await _effectivePermissionService.GetUserAccessSnapshotAsync(user.Id, cancellationToken);
            effectivePermissions = nativeSnapshot.EffectivePermissions.ToList();
            enabledModuleIds = nativeSnapshot.EnabledModules.Select(m => (int)m).ToList();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email!),
            new(ClaimTypes.GivenName, user.FirstName),
            new(ClaimTypes.Surname, user.LastName),
            new("tenant_id", user.TenantId.ToString()),
            new(AuthClaimTypes.TenantKind, homeTenant.Kind.ToClaimValue())
        };

        foreach (var role in roles)
        {
            if (string.Equals(role, PlatformRoles.PlatformAdmin, StringComparison.Ordinal))
                continue;
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        if (!claims.Exists(c => c.Type == ClaimTypes.Role))
            claims.Add(new Claim(ClaimTypes.Role, UserRole.Accountant.ToString()));

        foreach (var p in effectivePermissions)
            claims.Add(new Claim(AuthClaimTypes.Permission, p));

        if (isDelegated)
        {
            claims.Add(new Claim(AuthClaimTypes.AccessMode, "delegated"));
            claims.Add(new Claim(AuthClaimTypes.ContextTenantId, contextTenantId!.Value.ToString()));
            if (!string.IsNullOrEmpty(contextCompanyName))
                claims.Add(new Claim(AuthClaimTypes.ContextCompanyName, contextCompanyName));
            claims.Add(new Claim(AuthClaimTypes.IsFirmManaged, isFirmManaged ? "true" : "false"));
        }
        else
        {
            claims.Add(new Claim(AuthClaimTypes.AccessMode, "native"));
            nativeSnapshot ??= await _effectivePermissionService.GetUserAccessSnapshotAsync(user.Id, cancellationToken);
            if (nativeSnapshot.IsModulePermissionScoped)
                claims.Add(new Claim(AuthClaimTypes.PermissionSource, "modules"));
        }

        var jwtSettings = _configuration.GetSection("JwtSettings");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiry = DateTime.UtcNow.AddMinutes(int.Parse(jwtSettings["ExpiryMinutes"] ?? "15"));

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: expiry,
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

        user.RefreshToken = RefreshTokenHasher.Hash(refreshToken);
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(7);
        await _userManager.UpdateAsync(user);

        totalSw.Stop();
        _logger.LogDebug(
            "FirmRegistration.Step={Step} DurationMs={DurationMs} TenantId={TenantId} UserId={UserId}",
            "TokenGeneration",
            totalSw.ElapsedMilliseconds,
            homeTenantId,
            userId);

        return new AuthResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiry,
            User = CreateUserDto(user, homeTenant, roleEnum, enabledModuleIds, effectivePermissions, isDelegated, contextTenantId, contextCompanyName, isFirmManaged),
            Requires2Fa = false
        };
    }

    private static UserDto CreateUserDto(
        ApplicationUser user,
        Tenant homeTenant,
        UserRole roleEnum,
        IReadOnlyList<int> enabledModuleIds,
        IReadOnlyList<string> effectivePermissions,
        bool isDelegated,
        Guid? contextTenantId,
        string? contextCompanyName,
        bool isFirmManaged)
    {
        return new UserDto
        {
            Id = user.Id,
            Email = user.Email!,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = roleEnum,
            RoleDisplay = roleEnum.ToDisplayString(),
            TenantId = user.TenantId,
            CompanyName = homeTenant.CompanyName,
            TenantKind = homeTenant.Kind,
            TwoFactorEnabled = user.TwoFactorEnabled,
            EnabledModuleIds = enabledModuleIds.ToList(),
            EffectivePermissions = effectivePermissions.ToList(),
            AccessMode = isDelegated ? "delegated" : "native",
            ContextTenantId = isDelegated ? contextTenantId : null,
            ContextCompanyName = isDelegated ? contextCompanyName : null,
            IsFirmManaged = isDelegated && isFirmManaged
        };
    }
}
