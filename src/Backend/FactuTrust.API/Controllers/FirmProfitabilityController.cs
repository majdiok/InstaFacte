using System.Security.Claims;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

[ApiController]
[Route("api/firm/governance")]
[Authorize(Roles = $"{nameof(UserRole.FirmManager)},{nameof(UserRole.FirmAccountant)}")]
[Filters.RequireAccountingFirmsFeature]
public sealed class FirmProfitabilityController : ControllerBase
{
    private readonly IFirmTimeProfitabilityService _timeProfitability;
    private readonly IFirmCollaboratorRentabilityService _rentability;
    private readonly IFirmCollaboratorCostService _collaboratorCosts;
    private readonly IFirmCollaboratorCostSyncService _collaboratorCostSync;
    private readonly IFirmGovernanceFeature _feature;

    public FirmProfitabilityController(
        IFirmTimeProfitabilityService timeProfitability,
        IFirmCollaboratorRentabilityService rentability,
        IFirmCollaboratorCostService collaboratorCosts,
        IFirmCollaboratorCostSyncService collaboratorCostSync,
        IFirmGovernanceFeature feature)
    {
        _timeProfitability = timeProfitability;
        _rentability = rentability;
        _collaboratorCosts = collaboratorCosts;
        _collaboratorCostSync = collaboratorCostSync;
        _feature = feature;
    }

    [HttpGet("dossier-time-profitability")]
    [Authorize(Roles = nameof(UserRole.FirmManager))]
    public async Task<ActionResult<ApiResponse<FirmDossierTimeProfitabilityReportDto>>> GetDossierTimeProfitability(
        [FromQuery] string? company,
        [FromQuery] int? year,
        [FromQuery] Guid? collaboratorUserId,
        [FromQuery] FirmMarginSignFilter margin = FirmMarginSignFilter.All,
        CancellationToken cancellationToken = default)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var report = await _timeProfitability.GetDossierTimeProfitabilityAsync(
            tenantId.Value, company, year, collaboratorUserId, margin, cancellationToken);
        return Ok(ApiResponse<FirmDossierTimeProfitabilityReportDto>.Ok(report));
    }

    [HttpGet("dossier-time-profitability/export-pdf")]
    [Authorize(Roles = nameof(UserRole.FirmManager))]
    public async Task<IActionResult> ExportDossierTimeProfitabilityPdf(
        [FromQuery] string? company,
        [FromQuery] int? year,
        [FromQuery] Guid? collaboratorUserId,
        [FromQuery] FirmMarginSignFilter margin = FirmMarginSignFilter.All,
        CancellationToken cancellationToken = default)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var bytes = await _timeProfitability.ExportDossierTimeProfitabilityPdfAsync(
            tenantId.Value, company, year, collaboratorUserId, margin, cancellationToken);
        return File(bytes, "application/pdf", "rentabilite-dossiers.pdf");
    }

    [HttpPut("dossiers/{assignmentId:guid}/year-budgets/{year:int}")]
    [Authorize(Roles = nameof(UserRole.FirmManager))]
    public async Task<ActionResult<ApiResponse<FirmDossierYearBudgetDto>>> UpsertYearBudget(
        Guid assignmentId,
        int year,
        [FromBody] UpsertFirmDossierYearBudgetDto dto,
        CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var result = await _timeProfitability.UpsertYearBudgetAsync(
            tenantId.Value, assignmentId, year, dto.BudgetAnnuel, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<FirmDossierYearBudgetDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<FirmDossierYearBudgetDto>.Ok(result.Value));
    }

    [HttpGet("hourly-rate-settings")]
    public async Task<ActionResult<ApiResponse<FirmHourlyRateSettingsDto>>> GetHourlyRateSettings()
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var settings = await _timeProfitability.GetHourlyRateSettingsAsync();
        return Ok(ApiResponse<FirmHourlyRateSettingsDto>.Ok(settings));
    }

    // ============================================
    // COÛT EMPLOYEUR ET TAUX HORAIRE PAR COLLABORATEUR
    // ============================================

    [HttpGet("collaborator-costs/{year:int}")]
    [Authorize(Roles = nameof(UserRole.FirmManager))]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<FirmCollaboratorYearCostDto>>>> ListCollaboratorCosts(
        int year, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var rows = await _collaboratorCosts.ListAsync(tenantId.Value, year, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<FirmCollaboratorYearCostDto>>.Ok(rows));
    }

    [HttpPut("collaborator-costs/{year:int}/{collaboratorUserId:guid}")]
    [Authorize(Roles = nameof(UserRole.FirmManager))]
    public async Task<ActionResult<ApiResponse<FirmCollaboratorYearCostDto>>> SaveCollaboratorCost(
        int year,
        Guid collaboratorUserId,
        [FromBody] SaveFirmCollaboratorYearCostDto dto,
        CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var result = await _collaboratorCosts.SaveAsync(
            tenantId.Value, isManager: true, collaboratorUserId, year, dto, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<FirmCollaboratorYearCostDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<FirmCollaboratorYearCostDto>.Ok(result.Value, "Coût collaborateur enregistré."));
    }

    /// <summary>Salariés de la paie du cabinet, pour alimenter le sélecteur de liaison.</summary>
    [HttpGet("collaborator-costs/{year:int}/payroll-employees")]
    [Authorize(Roles = nameof(UserRole.FirmManager))]
    public async Task<ActionResult<ApiResponse<FirmPayrollCostSnapshotDto>>> ListPayrollEmployees(
        int year, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var snapshot = await _collaboratorCosts.GetPayrollEmployeesAsync(tenantId.Value, year, cancellationToken);
        return Ok(ApiResponse<FirmPayrollCostSnapshotDto>.Ok(snapshot));
    }

    [HttpPut("collaborator-costs/{collaboratorUserId:guid}/payroll-link")]
    [Authorize(Roles = nameof(UserRole.FirmManager))]
    public async Task<IActionResult> LinkPayrollEmployee(
        Guid collaboratorUserId,
        [FromBody] LinkFirmPayrollEmployeeDto dto,
        CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var result = await _collaboratorCosts.LinkPayrollEmployeeAsync(
            tenantId.Value, isManager: true, collaboratorUserId, dto.PayrollEmployeeId, cancellationToken: cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!, "Liaison paie enregistrée."));
    }

    [HttpPost("collaborator-costs/{year:int}/import-payroll")]
    [Authorize(Roles = nameof(UserRole.FirmManager))]
    public async Task<ActionResult<ApiResponse<FirmPayrollImportResultDto>>> ImportPayrollCosts(
        int year,
        [FromQuery] bool force = false,
        CancellationToken cancellationToken = default)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var sync = await _collaboratorCostSync.EnsureFreshAsync(
            tenantId.Value,
            year,
            FirmCostSyncTrigger.ManualImport,
            forceImport: force,
            cancellationToken);

        if (!sync.PayrollAvailable)
        {
            return Ok(ApiResponse<FirmPayrollImportResultDto>.Ok(
                new FirmPayrollImportResultDto
                {
                    PayrollAvailable = false,
                    UnavailableReason = sync.UnavailableReason
                },
                sync.UnavailableReason ?? "Paie du cabinet indisponible."));
        }

        var message = force
            ? $"{sync.Imported} collaborateur(s) mis à jour depuis la paie (import forcé)."
            : $"{sync.Imported} collaborateur(s) mis à jour depuis la paie.";
        return Ok(ApiResponse<FirmPayrollImportResultDto>.Ok(
            new FirmPayrollImportResultDto
            {
                PayrollAvailable = true,
                Imported = sync.Imported,
                Unlinked = sync.SkippedUnlinked,
                SkippedManual = sync.SkippedManual,
                SkippedUpToDate = sync.SkippedUpToDate
            },
            message));
    }

    [HttpPost("collaborator-costs/{year:int}/sync")]
    [Authorize(Roles = nameof(UserRole.FirmManager))]
    public async Task<ActionResult<ApiResponse<FirmCollaboratorCostSyncResultDto>>> SyncCollaboratorCosts(
        int year,
        [FromQuery] bool force = false,
        CancellationToken cancellationToken = default)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var result = await _collaboratorCostSync.EnsureFreshAsync(
            tenantId.Value,
            year,
            FirmCostSyncTrigger.ManualSync,
            forceImport: force,
            cancellationToken);
        return Ok(ApiResponse<FirmCollaboratorCostSyncResultDto>.Ok(result, "Synchronisation terminée."));
    }

    [HttpGet("rentability")]
    [Authorize(Roles = nameof(UserRole.FirmManager))]
    public async Task<ActionResult<ApiResponse<FirmCollaboratorRentabilityListDto>>> ListRentability(
        [FromQuery] int? year,
        [FromQuery] Guid? collaboratorUserId,
        CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var list = await _rentability.ListAsync(tenantId.Value, year, collaboratorUserId, cancellationToken);
        return Ok(ApiResponse<FirmCollaboratorRentabilityListDto>.Ok(list));
    }

    [HttpGet("rentability/{id:guid}")]
    [Authorize(Roles = nameof(UserRole.FirmManager))]
    public async Task<ActionResult<ApiResponse<FirmCollaboratorRentabilityDetailDto>>> GetRentability(
        Guid id, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var detail = await _rentability.GetByIdAsync(tenantId.Value, id, cancellationToken);
        if (detail is null) return NotFound(ApiResponse<FirmCollaboratorRentabilityDetailDto>.Fail("Rentabilité introuvable."));
        return Ok(ApiResponse<FirmCollaboratorRentabilityDetailDto>.Ok(detail));
    }

    [HttpGet("rentability/prefill")]
    [Authorize(Roles = nameof(UserRole.FirmManager))]
    public async Task<ActionResult<ApiResponse<FirmCollaboratorRentabilityDetailDto>>> PrefillRentability(
        [FromQuery] Guid collaboratorUserId,
        [FromQuery] int year,
        CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var result = await _rentability.GetPrefillAsync(tenantId.Value, collaboratorUserId, year, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<FirmCollaboratorRentabilityDetailDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<FirmCollaboratorRentabilityDetailDto>.Ok(result.Value));
    }

    [HttpPost("rentability")]
    [Authorize(Roles = nameof(UserRole.FirmManager))]
    public async Task<ActionResult<ApiResponse<FirmCollaboratorRentabilityDetailDto>>> CreateRentability(
        [FromBody] SaveFirmCollaboratorRentabilityDto dto, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var result = await _rentability.SaveAsync(tenantId.Value, dto, null, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<FirmCollaboratorRentabilityDetailDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<FirmCollaboratorRentabilityDetailDto>.Ok(result.Value));
    }

    [HttpPut("rentability/{id:guid}")]
    [Authorize(Roles = nameof(UserRole.FirmManager))]
    public async Task<ActionResult<ApiResponse<FirmCollaboratorRentabilityDetailDto>>> UpdateRentability(
        Guid id, [FromBody] SaveFirmCollaboratorRentabilityDto dto, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var result = await _rentability.SaveAsync(tenantId.Value, dto, id, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<FirmCollaboratorRentabilityDetailDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<FirmCollaboratorRentabilityDetailDto>.Ok(result.Value));
    }

    [HttpDelete("rentability/{id:guid}")]
    [Authorize(Roles = nameof(UserRole.FirmManager))]
    public async Task<IActionResult> DeleteRentability(Guid id, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var result = await _rentability.DeleteAsync(tenantId.Value, id, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!, "Rentabilité supprimée."));
    }

    [HttpPost("rentability/duplicate")]
    [Authorize(Roles = nameof(UserRole.FirmManager))]
    public async Task<ActionResult<ApiResponse<DuplicateFirmRentabilityResultDto>>> DuplicateRentability(
        [FromBody] ValidateTimeSheetsBulkDto dto, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var result = await _rentability.DuplicateAsync(tenantId.Value, dto.Ids, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<DuplicateFirmRentabilityResultDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<DuplicateFirmRentabilityResultDto>.Ok(result.Value));
    }

    /// <summary>
    /// Recalcule toutes les marges enregistrées selon le modèle de marge sur coût direct.
    /// </summary>
    /// <remarks>
    /// Action explicite du manager : elle modifie des chiffres déjà communiqués. La valeur
    /// antérieure de chaque snapshot est conservée, et les exercices sans feuille de temps sont
    /// laissés intacts.
    /// </remarks>
    [HttpPost("rentability/recalculate")]
    [Authorize(Roles = nameof(UserRole.FirmManager))]
    public async Task<ActionResult<ApiResponse<FirmRentabilityRecalculationResultDto>>> RecalculateRentability(
        CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var result = await _rentability.RecalculateAllAsync(tenantId.Value, isManager: true, cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<FirmRentabilityRecalculationResultDto>.Fail(result.Error.Description));

        var message = result.Value.Skipped.Count == 0
            ? $"{result.Value.Recalculated} marge(s) recalculée(s)."
            : $"{result.Value.Recalculated} marge(s) recalculée(s), {result.Value.Skipped.Count} laissée(s) intacte(s).";
        return Ok(ApiResponse<FirmRentabilityRecalculationResultDto>.Ok(result.Value, message));
    }

    private bool EnsureEnabled(out ActionResult? disabledResult)
    {
        if (_feature.IsEnabled)
        {
            disabledResult = null;
            return true;
        }
        disabledResult = StatusCode(503, ApiResponse<object>.Fail("Module gouvernance cabinet désactivé."));
        return false;
    }

    private Guid? GetHomeTenantId()
    {
        var claim = User.FindFirstValue("tenant_id");
        return Guid.TryParse(claim, out var id) ? id : null;
    }
}
