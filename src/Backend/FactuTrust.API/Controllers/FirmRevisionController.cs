using System.Security.Claims;
using FactuTrust.API.Authorization;
using FactuTrust.Application.Common;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FactuTrust.API.Controllers;

/// <summary>
/// Réviseur de portefeuille cabinet : consolidation des anomalies détectées dans chaque dossier,
/// file de travail priorisée, dossier de révision.
///
/// <para>Toutes les routes répondent <b>503</b> tant que
/// <c>Features:AccountingFirms:FirmRevisionEnabled</c> est faux — même patron que la trésorerie
/// prévisionnelle. Le drapeau back et le drapeau front doivent être basculés ensemble.</para>
/// </summary>
[ApiController]
[Route("api/firm/revision")]
[Authorize(Roles = $"{nameof(UserRole.FirmManager)},{nameof(UserRole.FirmAccountant)}")]
[Filters.RequireAccountingFirmsFeature]
public sealed class FirmRevisionController : ControllerBase
{
    private const string DisabledMessage =
        "Le réviseur de portefeuille n'est pas activé sur cette instance.";

    private readonly IFirmRevisionService _revision;
    private readonly ICurrentUser _currentUser;
    private readonly AccountingFirmsOptions _options;

    public FirmRevisionController(
        IFirmRevisionService revision,
        ICurrentUser currentUser,
        IOptions<AccountingFirmsOptions> options)
    {
        _revision = revision;
        _currentUser = currentUser;
        _options = options.Value;
    }

    [HttpGet("overview")]
    [Authorize(Policy = PermissionPolicies.FirmRevisionView)]
    public async Task<IActionResult> GetOverview([FromQuery] int fiscalYear, CancellationToken ct)
    {
        if (Disabled(out var disabled)) return disabled;
        if (!TryResolveContext(out var firmTenantId, out var scope, out var denied)) return denied;

        var result = await _revision.GetOverviewAsync(firmTenantId, scope, ResolveYear(fiscalYear), ct);
        return result.IsFailure
            ? BadRequest(ApiResponse<object>.Fail(result.Error.Description))
            : Ok(ApiResponse<FirmRevisionOverviewDto>.Ok(result.Value));
    }

    [HttpGet("dossiers/{companyTenantId:guid}")]
    [Authorize(Policy = PermissionPolicies.FirmRevisionView)]
    public async Task<IActionResult> GetDossier(
        Guid companyTenantId, [FromQuery] int fiscalYear, CancellationToken ct)
    {
        if (Disabled(out var disabled)) return disabled;
        if (!TryResolveContext(out var firmTenantId, out var scope, out var denied)) return denied;

        var result = await _revision.GetDossierAsync(
            firmTenantId, scope, companyTenantId, ResolveYear(fiscalYear), ct);
        return result.IsFailure
            ? BadRequest(ApiResponse<object>.Fail(result.Error.Description))
            : Ok(ApiResponse<FirmRevisionDossierDetailDto>.Ok(result.Value));
    }

    [HttpGet("work-queue")]
    [Authorize(Policy = PermissionPolicies.FirmRevisionView)]
    public async Task<IActionResult> GetWorkQueue([FromQuery] int fiscalYear, CancellationToken ct)
    {
        if (Disabled(out var disabled)) return disabled;
        if (!TryResolveContext(out var firmTenantId, out var scope, out var denied)) return denied;

        var result = await _revision.GetWorkQueueAsync(firmTenantId, scope, ResolveYear(fiscalYear), ct);
        return result.IsFailure
            ? BadRequest(ApiResponse<object>.Fail(result.Error.Description))
            : Ok(ApiResponse<FirmRevisionWorkQueueDto>.Ok(result.Value));
    }

    /// <summary>
    /// Produit ou régénère le dossier de révision d'un dossier client. Réservé au responsable :
    /// la génération peut solliciter le modèle de langage.
    /// </summary>
    [HttpPost("dossiers/{companyTenantId:guid}/note")]
    [Authorize(Policy = PermissionPolicies.FirmRevisionManage)]
    public async Task<IActionResult> GenerateNote(
        Guid companyTenantId, [FromQuery] int fiscalYear, CancellationToken ct)
    {
        if (Disabled(out var disabled)) return disabled;
        if (!TryResolveContext(out var firmTenantId, out var scope, out var denied)) return denied;

        var result = await _revision.GenerateNoteAsync(
            firmTenantId, scope, companyTenantId, ResolveYear(fiscalYear),
            _currentUser.UserId, _currentUser.Email, ct);

        return result.IsFailure
            ? BadRequest(ApiResponse<object>.Fail(result.Error.Description))
            : Ok(ApiResponse<FirmRevisionNoteDto>.Ok(result.Value));
    }

    /// <summary>
    /// Télécharge le dossier de révision au format PDF. Consultation : un collaborateur affecté au
    /// dossier peut l'obtenir, l'ACL dossier s'appliquant en amont.
    /// </summary>
    [HttpGet("dossiers/{companyTenantId:guid}/note/export")]
    [Authorize(Policy = PermissionPolicies.FirmRevisionView)]
    public async Task<IActionResult> ExportNote(
        Guid companyTenantId, [FromQuery] int fiscalYear, CancellationToken ct)
    {
        if (Disabled(out var disabled)) return disabled;
        if (!TryResolveContext(out var firmTenantId, out var scope, out var denied)) return denied;

        var year = ResolveYear(fiscalYear);
        var result = await _revision.ExportNoteAsync(firmTenantId, scope, companyTenantId, year, ct);

        if (result.IsFailure)
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));

        return File(result.Value, "application/pdf", $"dossier-revision-{year}.pdf");
    }

    /// <summary>
    /// Lance un contrôle sur chaque dossier du périmètre. Réservé au responsable de cabinet : un
    /// balayage ouvre une connexion par dossier.
    /// </summary>
    [HttpPost("sweep")]
    [Authorize(Policy = PermissionPolicies.FirmRevisionManage)]
    public async Task<IActionResult> Sweep([FromQuery] int fiscalYear, CancellationToken ct)
    {
        if (Disabled(out var disabled)) return disabled;
        if (!TryResolveContext(out var firmTenantId, out var scope, out var denied)) return denied;

        var result = await _revision.SweepAsync(
            firmTenantId, scope, ResolveYear(fiscalYear),
            _currentUser.UserId, _currentUser.Email, ct);

        return result.IsFailure
            ? BadRequest(ApiResponse<object>.Fail(result.Error.Description))
            : Ok(ApiResponse<FirmRevisionSweepResultDto>.Ok(result.Value));
    }

    // ── Garde-fous ────────────────────────────────────────────────────────────────────────

    private bool Disabled(out IActionResult result)
    {
        if (_options.FirmRevisionEnabled)
        {
            result = Ok();
            return false;
        }

        result = StatusCode(StatusCodes.Status503ServiceUnavailable,
            ApiResponse<object>.Fail(DisabledMessage));
        return true;
    }

    /// <summary>
    /// Cabinet courant et périmètre d'accès. <b>Fail-closed</b> : un utilisateur cabinet dont le
    /// périmètre ne peut pas être établi n'obtient pas le portefeuille complet par défaut — il est
    /// refusé.
    /// </summary>
    private bool TryResolveContext(
        out Guid firmTenantId,
        out FirmDossierAccessScope? scope,
        out IActionResult denied)
    {
        firmTenantId = Guid.Empty;
        scope = null;
        denied = Unauthorized();

        var claim = User.FindFirstValue("tenant_id");
        if (!Guid.TryParse(claim, out var tenantId) || tenantId == Guid.Empty)
            return false;

        if (!_currentUser.TryGetAccessScope(out var resolved))
            return false;

        firmTenantId = tenantId;
        scope = resolved;
        return true;
    }

    /// <summary>Exercice courant quand l'appelant n'en précise pas.</summary>
    private static int ResolveYear(int fiscalYear) =>
        fiscalYear is >= 2000 and <= 2100 ? fiscalYear : DateTime.UtcNow.Year;
}
