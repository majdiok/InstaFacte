using System.Security.Claims;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Auth;
using FactuTrust.Domain.Enums;
using FactuTrust.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FactuTrust.Infrastructure.Services;

public sealed class FirmContextService : IFirmContextService
{
    private readonly MasterDbContext _masterContext;
    private readonly IFirmAssignmentService _assignmentService;
    private readonly ITenantAuthTokenService _tokenService;

    public FirmContextService(
        MasterDbContext masterContext,
        IFirmAssignmentService assignmentService,
        ITenantAuthTokenService tokenService)
    {
        _masterContext = masterContext;
        _assignmentService = assignmentService;
        _tokenService = tokenService;
    }

    public async Task<AuthResponseDto> SwitchToClientAsync(
        Guid userId, Guid homeTenantId, Guid clientTenantId, CancellationToken cancellationToken = default)
    {
        var homeTenant = await _masterContext.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == homeTenantId, cancellationToken)
            ?? throw new InvalidOperationException("Cabinet introuvable.");

        if (homeTenant.Kind != TenantKind.AccountingFirm)
            throw new InvalidOperationException("Seul un cabinet comptable peut basculer vers un dossier client.");

        var hasAssignment = await _assignmentService.HasActiveAssignmentAsync(homeTenantId, clientTenantId, cancellationToken);
        if (!hasAssignment)
            throw new UnauthorizedAccessException("Aucune affectation active pour cette société.");

        var clientTenant = await _masterContext.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == clientTenantId && t.IsActive, cancellationToken)
            ?? throw new UnauthorizedAccessException("Société cliente introuvable ou inactive.");

        return await _tokenService.GenerateTokensAsync(
            userId, homeTenantId, clientTenantId, clientTenant.CompanyName, cancellationToken);
    }

    public Task<AuthResponseDto> ClearContextAsync(
        Guid userId, Guid homeTenantId, CancellationToken cancellationToken = default)
    {
        return _tokenService.GenerateTokensAsync(userId, homeTenantId, cancellationToken: cancellationToken);
    }

    public Task<FirmContextDto> GetCurrentContextAsync(
        ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        var accessMode = principal.FindFirstValue(AuthClaimTypes.AccessMode) ?? "native";
        var contextTenantIdClaim = principal.FindFirstValue(AuthClaimTypes.ContextTenantId);
        Guid? contextTenantId = Guid.TryParse(contextTenantIdClaim, out var id) ? id : null;
        var contextCompanyName = principal.FindFirstValue(AuthClaimTypes.ContextCompanyName);

        return Task.FromResult(new FirmContextDto
        {
            AccessMode = accessMode,
            ClientTenantId = contextTenantId,
            ClientCompanyName = contextCompanyName
        });
    }
}
