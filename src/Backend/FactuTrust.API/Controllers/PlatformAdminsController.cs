using System.Security.Claims;
using FactuTrust.API.Authorization;
using FactuTrust.Application.Common.Logging;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Auth;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.API.Controllers;

/// <summary>
/// Lot B1 — Gestion des administrateurs plateforme (CRUD léger).
///
/// Toutes les actions exigent la permission <see cref="PlatformPermissions.AdminsManage"/>
/// sauf la lecture qui exige <see cref="PlatformPermissions.AdminsRead"/>. La création/
/// modification d'admin n'est possible que par un <see cref="PlatformRoles.PlatformAdmin"/>
/// (super-admin) — protégé par la matrice <see cref="RolePermissionMatrix"/> qui
/// n'attribue <c>admins:manage</c> qu'au super-admin.
///
/// <b>Hors périmètre Lot B1</b> : invitation par email (différée Lot C2 quand
/// <c>IEmailService</c> sera réel), 2FA TOTP (Lot B2), audit log dédié des actions
/// admin (Lot B3 — pour l'instant on logge via <c>ILogger</c>).
/// </summary>
[ApiController]
[Route("api/platform/admins")]
[Authorize(Policy = PlatformPolicies.PlatformAdmin)]
public sealed class PlatformAdminsController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly MasterDbContext _masterContext;
    private readonly ILogger<PlatformAdminsController> _logger;

    public PlatformAdminsController(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        MasterDbContext masterContext,
        ILogger<PlatformAdminsController> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _masterContext = masterContext;
        _logger = logger;
    }

    /// <summary>Liste tous les administrateurs plateforme + KPIs agrégés.</summary>
    [HttpGet]
    [Authorize(Policy = "perm:" + PlatformPermissions.AdminsRead)]
    [ProducesResponseType(typeof(ApiResponse<PlatformAdminListPageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var users = await _masterContext.Users
            .AsNoTracking()
            .Where(u => u.TenantId == Guid.Empty)
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .ToListAsync(cancellationToken);

        var items = new List<PlatformAdminListItemDto>(users.Count);
        var nowUtc = DateTime.UtcNow;
        var lockedCount = 0;
        var superAdminCount = 0;
        var activeCount = 0;

        foreach (var user in users)
        {
            var allRoles = await _userManager.GetRolesAsync(user);
            var platformRoles = allRoles.Where(PlatformRoles.IsKnownRole).ToList();
            if (platformRoles.Count == 0)
            {
                // Skip : utilisateur master sans rôle plateforme (cas legacy / scripts)
                continue;
            }

            var isLockedOut = user.LockoutEnd.HasValue && user.LockoutEnd > nowUtc;
            if (isLockedOut) lockedCount++;
            if (user.IsActive) activeCount++;
            if (platformRoles.Contains(PlatformRoles.PlatformAdmin)) superAdminCount++;

            items.Add(new PlatformAdminListItemDto
            {
                Id = user.Id,
                Email = user.Email!,
                FirstName = user.FirstName,
                LastName = user.LastName,
                IsActive = user.IsActive,
                LastLoginAt = user.LastLoginAt,
                Roles = platformRoles,
                IsLockedOut = isLockedOut,
                CreatedAt = user.LockoutEnd?.UtcDateTime ?? DateTime.UtcNow // placeholder ; ApplicationUser n'a pas de CreatedAt direct
            });
        }

        var page = new PlatformAdminListPageDto
        {
            Items = items,
            TotalCount = items.Count,
            ActiveCount = activeCount,
            LockedCount = lockedCount,
            SuperAdminCount = superAdminCount
        };

        return Ok(ApiResponse<PlatformAdminListPageDto>.Ok(page));
    }

    /// <summary>Crée un nouvel administrateur plateforme (mot de passe initial saisi par le super-admin).</summary>
    [HttpPost]
    [Authorize(Policy = "perm:" + PlatformPermissions.AdminsManage)]
    [ProducesResponseType(typeof(ApiResponse<PlatformAdminListItemDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePlatformAdminRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<PlatformAdminListItemDto>.Fail("Requête invalide."));

        if (!PlatformRoles.IsKnownRole(request.Role))
            return BadRequest(ApiResponse<PlatformAdminListItemDto>.Fail(
                "Rôle inconnu. Valeurs acceptées : PlatformAdmin, BillingAdmin, SupportAgent, MigrationOperator, ReadOnlyAuditor."));

        var existing = await _userManager.FindByEmailAsync(request.Email);
        if (existing is not null)
            return BadRequest(ApiResponse<PlatformAdminListItemDto>.Fail("Un compte existe déjà avec cet email."));

        // S'assure que le rôle existe dans Identity (idempotent).
        await EnsureRoleExistsAsync(request.Role);

        var user = new ApplicationUser
        {
            Email = request.Email.Trim().ToLowerInvariant(),
            UserName = request.Email.Trim().ToLowerInvariant(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            TenantId = Guid.Empty,
            IsActive = true,
            EmailConfirmed = true // pas de vérif email tant que le service email n'est pas réel (Lot C2)
        };

        var createResult = await _userManager.CreateAsync(user, request.InitialPassword);
        if (!createResult.Succeeded)
        {
            return BadRequest(ApiResponse<PlatformAdminListItemDto>.Fail(
                string.Join(' ', createResult.Errors.Select(e => e.Description))));
        }

        var addRoleResult = await _userManager.AddToRoleAsync(user, request.Role);
        if (!addRoleResult.Succeeded)
        {
            // Rollback : supprime l'utilisateur si l'attribution de rôle échoue
            await _userManager.DeleteAsync(user);
            return BadRequest(ApiResponse<PlatformAdminListItemDto>.Fail(
                string.Join(' ', addRoleResult.Errors.Select(e => e.Description))));
        }

        _logger.LogInformation(
            "Platform admin {ActorId} created new admin {NewAdminEmail} with role {Role}",
            CurrentUserId(), LogSanitizer.MaskEmail(user.Email), request.Role);

        return CreatedAtAction(nameof(List), null, ApiResponse<PlatformAdminListItemDto>.Ok(
            new PlatformAdminListItemDto
            {
                Id = user.Id,
                Email = user.Email!,
                FirstName = user.FirstName,
                LastName = user.LastName,
                IsActive = user.IsActive,
                LastLoginAt = null,
                Roles = new[] { request.Role },
                IsLockedOut = false,
                CreatedAt = DateTime.UtcNow
            },
            "Administrateur créé."));
    }

    /// <summary>Change le rôle d'un administrateur existant (retire les anciens rôles plateforme).</summary>
    [HttpPost("{id:guid}/role")]
    [Authorize(Policy = "perm:" + PlatformPermissions.AdminsManage)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeRole(
        Guid id,
        [FromBody] ChangePlatformAdminRoleRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Fail("Requête invalide."));

        if (!PlatformRoles.IsKnownRole(request.Role))
            return BadRequest(ApiResponse<object>.Fail("Rôle inconnu."));

        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null || user.TenantId != Guid.Empty)
            return NotFound(ApiResponse<object>.Fail("Administrateur introuvable."));

        var allRoles = await _userManager.GetRolesAsync(user);
        var currentPlatformRoles = allRoles.Where(PlatformRoles.IsKnownRole).ToList();

        // Garde-fou : empêche de retirer le dernier SuperAdmin actif.
        if (currentPlatformRoles.Contains(PlatformRoles.PlatformAdmin)
            && request.Role != PlatformRoles.PlatformAdmin)
        {
            var otherSuperAdmins = await CountOtherActiveSuperAdminsAsync(user.Id, cancellationToken);
            if (otherSuperAdmins == 0)
            {
                return BadRequest(ApiResponse<object>.Fail(
                    "Impossible : il doit toujours rester au moins un super-administrateur actif."));
            }
        }

        if (currentPlatformRoles.Count > 0)
        {
            var removeResult = await _userManager.RemoveFromRolesAsync(user, currentPlatformRoles);
            if (!removeResult.Succeeded)
            {
                return BadRequest(ApiResponse<object>.Fail(
                    string.Join(' ', removeResult.Errors.Select(e => e.Description))));
            }
        }

        await EnsureRoleExistsAsync(request.Role);
        var addResult = await _userManager.AddToRoleAsync(user, request.Role);
        if (!addResult.Succeeded)
        {
            return BadRequest(ApiResponse<object>.Fail(
                string.Join(' ', addResult.Errors.Select(e => e.Description))));
        }

        // Force la re-connexion : invalide le refresh token.
        user.RefreshToken = null;
        user.RefreshTokenExpiryTime = null;
        await _userManager.UpdateAsync(user);

        _logger.LogInformation(
            "Platform admin {ActorId} changed role of {TargetEmail} to {Role}",
            CurrentUserId(), LogSanitizer.MaskEmail(user.Email), request.Role);

        return Ok(ApiResponse<object>.Ok(null!, "Rôle mis à jour. L'utilisateur devra se reconnecter."));
    }

    /// <summary>Désactive un administrateur (lui interdit le login).</summary>
    [HttpPost("{id:guid}/disable")]
    [Authorize(Policy = "perm:" + PlatformPermissions.AdminsManage)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Disable(Guid id, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null || user.TenantId != Guid.Empty)
            return NotFound(ApiResponse<object>.Fail("Administrateur introuvable."));

        if (user.Id.ToString() == CurrentUserId())
            return BadRequest(ApiResponse<object>.Fail("Vous ne pouvez pas vous désactiver vous-même."));

        var allRoles = await _userManager.GetRolesAsync(user);
        if (allRoles.Contains(PlatformRoles.PlatformAdmin))
        {
            var otherSuperAdmins = await CountOtherActiveSuperAdminsAsync(user.Id, cancellationToken);
            if (otherSuperAdmins == 0)
                return BadRequest(ApiResponse<object>.Fail(
                    "Impossible : il doit toujours rester au moins un super-administrateur actif."));
        }

        user.IsActive = false;
        user.RefreshToken = null;
        user.RefreshTokenExpiryTime = null;
        await _userManager.UpdateAsync(user);

        _logger.LogInformation(
            "Platform admin {ActorId} disabled {TargetEmail}",
            CurrentUserId(), LogSanitizer.MaskEmail(user.Email));

        return Ok(ApiResponse<object>.Ok(null!, "Administrateur désactivé."));
    }

    /// <summary>Réactive un administrateur précédemment désactivé.</summary>
    [HttpPost("{id:guid}/enable")]
    [Authorize(Policy = "perm:" + PlatformPermissions.AdminsManage)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Enable(Guid id, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null || user.TenantId != Guid.Empty)
            return NotFound(ApiResponse<object>.Fail("Administrateur introuvable."));

        user.IsActive = true;
        await _userManager.UpdateAsync(user);

        _logger.LogInformation(
            "Platform admin {ActorId} enabled {TargetEmail}",
            CurrentUserId(), LogSanitizer.MaskEmail(user.Email));

        return Ok(ApiResponse<object>.Ok(null!, "Administrateur réactivé."));
    }

    /// <summary>Réinitialise le mot de passe d'un administrateur.</summary>
    [HttpPost("{id:guid}/reset-password")]
    [Authorize(Policy = "perm:" + PlatformPermissions.AdminsManage)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetPassword(
        Guid id,
        [FromBody] ResetPlatformAdminPasswordRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Fail("Requête invalide."));

        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null || user.TenantId != Guid.Empty)
            return NotFound(ApiResponse<object>.Fail("Administrateur introuvable."));

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var resetResult = await _userManager.ResetPasswordAsync(user, token, request.NewPassword);
        if (!resetResult.Succeeded)
        {
            return BadRequest(ApiResponse<object>.Fail(
                string.Join(' ', resetResult.Errors.Select(e => e.Description))));
        }

        // Force la re-connexion : invalide le refresh token + clear lockout
        user.RefreshToken = null;
        user.RefreshTokenExpiryTime = null;
        user.LockoutEnd = null;
        await _userManager.UpdateAsync(user);
        await _userManager.ResetAccessFailedCountAsync(user);

        _logger.LogInformation(
            "Platform admin {ActorId} reset password for {TargetEmail}",
            CurrentUserId(), LogSanitizer.MaskEmail(user.Email));

        return Ok(ApiResponse<object>.Ok(null!, "Mot de passe réinitialisé. L'utilisateur devra se reconnecter."));
    }

    /// <summary>Compte les super-admins actifs autres que celui visé (pour le garde-fou de désactivation).</summary>
    private async Task<int> CountOtherActiveSuperAdminsAsync(Guid excludingUserId, CancellationToken cancellationToken)
    {
        var role = await _roleManager.FindByNameAsync(PlatformRoles.PlatformAdmin);
        if (role is null) return 0;

        return await _masterContext.UserRoles
            .Where(ur => ur.RoleId == role.Id)
            .Join(_masterContext.Users,
                ur => ur.UserId,
                u => u.Id,
                (ur, u) => u)
            .Where(u => u.Id != excludingUserId && u.IsActive && u.TenantId == Guid.Empty)
            .CountAsync(cancellationToken);
    }

    private async Task EnsureRoleExistsAsync(string roleName)
    {
        if (!await _roleManager.RoleExistsAsync(roleName))
        {
            await _roleManager.CreateAsync(new ApplicationRole { Name = roleName });
        }
    }

    private string? CurrentUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
}
