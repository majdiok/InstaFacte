using System.Security.Claims;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>
/// Création par le cabinet de dossiers clients gérés (sociétés sans compte plateforme),
/// à la manière d'un logiciel comptable classique (nouveau dossier comptable).
/// </summary>
[ApiController]
[Route("api/firm/managed-clients")]
[Authorize(Roles = nameof(UserRole.FirmManager))]
[Filters.RequireAccountingFirmsFeature]
public sealed class FirmManagedClientsController : ControllerBase
{
    private readonly IFirmManagedClientService _managedClients;

    public FirmManagedClientsController(IFirmManagedClientService managedClients)
    {
        _managedClients = managedClients;
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<FirmManagedClientCreatedDto>>> Create(
        [FromBody] CreateFirmManagedClientDto dto,
        CancellationToken cancellationToken)
    {
        var tenantId = GetHomeTenantId();
        var userId = GetUserId();
        if (tenantId is null || userId is null)
            return Unauthorized();

        var result = await _managedClients.CreateManagedClientAsync(tenantId.Value, userId.Value, dto, cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<FirmManagedClientCreatedDto>.Fail(result.Error.Description));

        return Ok(ApiResponse<FirmManagedClientCreatedDto>.Ok(result.Value, "Dossier client créé"));
    }

    private Guid? GetHomeTenantId()
    {
        var claim = User.FindFirstValue("tenant_id");
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
