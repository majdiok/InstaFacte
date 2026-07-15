using System.Security.Claims;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using FactuTrust.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.API.Controllers;

[ApiController]
[Route("api/firm/users")]
[Authorize(Roles = nameof(UserRole.FirmManager))]
[Filters.RequireAccountingFirmsFeature]
public sealed class FirmUsersController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly MasterDbContext _masterContext;

    public FirmUsersController(UserManager<ApplicationUser> userManager, MasterDbContext masterContext)
    {
        _userManager = userManager;
        _masterContext = masterContext;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<FirmUserDto>>>> List(CancellationToken cancellationToken)
    {
        var tenantId = GetHomeTenantId();
        if (tenantId is null)
            return Unauthorized();

        var users = await _masterContext.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId.Value)
            .OrderBy(u => u.LastName)
            .ToListAsync(cancellationToken);

        var result = new List<FirmUserDto>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var roleName = roles.FirstOrDefault() ?? UserRole.FirmAccountant.ToString();
            var role = Enum.TryParse<UserRole>(roleName, out var r) ? r : UserRole.FirmAccountant;

            result.Add(new FirmUserDto
            {
                Id = user.Id,
                Email = user.Email!,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Role = role,
                RoleDisplay = role.ToDisplayString(),
                IsActive = user.IsActive
            });
        }

        return Ok(ApiResponse<IReadOnlyList<FirmUserDto>>.Ok(result));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<FirmUserDto>>> Create(
        [FromBody] CreateFirmUserDto dto,
        CancellationToken cancellationToken)
    {
        var tenantId = GetHomeTenantId();
        if (tenantId is null)
            return Unauthorized();

        if (dto.Role is not (UserRole.FirmManager or UserRole.FirmAccountant))
            return BadRequest(ApiResponse<FirmUserDto>.Fail("Rôle cabinet invalide"));

        var tenant = await _masterContext.Tenants.FindAsync([tenantId.Value], cancellationToken);
        if (tenant is null || tenant.Kind != TenantKind.AccountingFirm)
            return BadRequest(ApiResponse<FirmUserDto>.Fail("Contexte cabinet invalide"));

        var user = new ApplicationUser
        {
            UserName = dto.Email,
            Email = dto.Email,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            TenantId = tenantId.Value,
            EmailConfirmed = true,
            IsActive = true
        };

        var createResult = await _userManager.CreateAsync(user, dto.Password);
        if (!createResult.Succeeded)
        {
            var errors = IdentityErrorTranslator.TranslateToFrench(createResult.Errors);
            return BadRequest(ApiResponse<FirmUserDto>.Fail(errors));
        }

        await _userManager.AddToRoleAsync(user, dto.Role.ToString());

        return Ok(ApiResponse<FirmUserDto>.Ok(new FirmUserDto
        {
            Id = user.Id,
            Email = user.Email!,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Role = dto.Role,
            RoleDisplay = dto.Role.ToDisplayString(),
            IsActive = user.IsActive
        }, "Utilisateur créé"));
    }

    private Guid? GetHomeTenantId()
    {
        var claim = User.FindFirstValue("tenant_id");
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
