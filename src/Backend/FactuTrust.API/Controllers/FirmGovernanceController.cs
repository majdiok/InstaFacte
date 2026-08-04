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
public sealed class FirmGovernanceController : ControllerBase
{
    private readonly IFirmGovernanceService _governance;
    private readonly IFirmGovernanceFeature _feature;

    public FirmGovernanceController(IFirmGovernanceService governance, IFirmGovernanceFeature feature)
    {
        _governance = governance;
        _feature = feature;
    }

    [HttpGet("dashboard")]
    public async Task<ActionResult<ApiResponse<FirmGovernanceDashboardDto>>> GetDashboard(CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var dto = await _governance.GetGovernanceDashboardAsync(tenantId.Value, cancellationToken);
        return Ok(ApiResponse<FirmGovernanceDashboardDto>.Ok(dto));
    }

    [HttpGet("permanent-files")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PermanentFileDto>>>> ListPermanentFiles(CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var list = await _governance.ListPermanentFilesAsync(tenantId.Value, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<PermanentFileDto>>.Ok(list));
    }

    [HttpGet("permanent-files/{assignmentId:guid}")]
    public async Task<ActionResult<ApiResponse<PermanentFileDto>>> GetPermanentFile(Guid assignmentId, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var file = await _governance.GetPermanentFileAsync(tenantId.Value, assignmentId, cancellationToken);
        if (file is null) return NotFound(ApiResponse<PermanentFileDto>.Fail("Dossier permanent introuvable."));
        return Ok(ApiResponse<PermanentFileDto>.Ok(file));
    }

    [HttpPut("permanent-files/{assignmentId:guid}")]
    public async Task<ActionResult<ApiResponse<PermanentFileDto>>> UpsertPermanentFile(
        Guid assignmentId, [FromBody] UpsertPermanentFileDto dto, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var result = await _governance.UpsertPermanentFileAsync(tenantId.Value, assignmentId, dto, User.Identity?.Name ?? "system", cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<PermanentFileDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<PermanentFileDto>.Ok(result.Value));
    }

    [HttpPost("permanent-files/{assignmentId:guid}/sync")]
    public async Task<IActionResult> SyncPermanentFile(Guid assignmentId, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var result = await _governance.SyncPermanentFileToTenantAsync(tenantId.Value, assignmentId, User.Identity?.Name ?? "system", cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!, "Synchronisation effectuée."));
    }

    [HttpPost("permanent-files/{assignmentId:guid}/representatives")]
    public async Task<ActionResult<ApiResponse<LegalRepresentativeDto>>> AddRepresentative(
        Guid assignmentId, [FromBody] LegalRepresentativeDto dto, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var result = await _governance.AddRepresentativeAsync(tenantId.Value, assignmentId, dto, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<LegalRepresentativeDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<LegalRepresentativeDto>.Ok(result.Value));
    }

    [HttpPut("permanent-files/{assignmentId:guid}/representatives/{representativeId:guid}")]
    public async Task<ActionResult<ApiResponse<LegalRepresentativeDto>>> UpdateRepresentative(
        Guid assignmentId, Guid representativeId, [FromBody] LegalRepresentativeDto dto, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var result = await _governance.UpdateRepresentativeAsync(tenantId.Value, assignmentId, representativeId, dto, cancellationToken);
        if (result.IsFailure)
        {
            if (result.Error.Code.Contains("NotFound", StringComparison.OrdinalIgnoreCase))
                return NotFound(ApiResponse<LegalRepresentativeDto>.Fail(result.Error.Description));
            return BadRequest(ApiResponse<LegalRepresentativeDto>.Fail(result.Error.Description));
        }
        return Ok(ApiResponse<LegalRepresentativeDto>.Ok(result.Value));
    }

    [HttpPost("permanent-files/{assignmentId:guid}/shareholders")]
    public async Task<ActionResult<ApiResponse<ShareholderDto>>> AddShareholder(
        Guid assignmentId, [FromBody] ShareholderDto dto, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var result = await _governance.AddShareholderAsync(tenantId.Value, assignmentId, dto, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<ShareholderDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<ShareholderDto>.Ok(result.Value));
    }

    [HttpPut("permanent-files/{assignmentId:guid}/shareholders/{shareholderId:guid}")]
    public async Task<ActionResult<ApiResponse<ShareholderDto>>> UpdateShareholder(
        Guid assignmentId, Guid shareholderId, [FromBody] ShareholderDto dto, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var result = await _governance.UpdateShareholderAsync(tenantId.Value, assignmentId, shareholderId, dto, cancellationToken);
        if (result.IsFailure)
        {
            if (result.Error.Code.Contains("NotFound", StringComparison.OrdinalIgnoreCase))
                return NotFound(ApiResponse<ShareholderDto>.Fail(result.Error.Description));
            return BadRequest(ApiResponse<ShareholderDto>.Fail(result.Error.Description));
        }
        return Ok(ApiResponse<ShareholderDto>.Ok(result.Value));
    }

    [HttpPost("permanent-files/{assignmentId:guid}/representatives/{representativeId:guid}/deactivate")]
    public async Task<IActionResult> DeactivateRepresentative(Guid assignmentId, Guid representativeId, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var result = await _governance.DeactivateRepresentativeAsync(tenantId.Value, assignmentId, representativeId, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!, "Dirigeant désactivé."));
    }

    [HttpPost("permanent-files/{assignmentId:guid}/shareholders/{shareholderId:guid}/deactivate")]
    public async Task<IActionResult> DeactivateShareholder(Guid assignmentId, Guid shareholderId, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var result = await _governance.DeactivateShareholderAsync(tenantId.Value, assignmentId, shareholderId, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!, "Associé désactivé."));
    }

    [HttpPost("permanent-files/{assignmentId:guid}/archive")]
    public async Task<IActionResult> ArchivePermanentFile(Guid assignmentId, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var result = await _governance.ArchivePermanentFileAsync(tenantId.Value, assignmentId, User.Identity?.Name ?? "system", cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!, "Dossier archivé."));
    }

    [HttpGet("time-sheets")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<FirmTimeSheetEntryDto>>>> ListTimeSheets(
        [FromQuery] int? year,
        [FromQuery] int? month,
        [FromQuery] Guid? userId,
        [FromQuery] Guid? assignmentId,
        CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var list = await _governance.ListTimeSheetsAsync(tenantId.Value, year, month, userId, assignmentId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<FirmTimeSheetEntryDto>>.Ok(list));
    }

    [HttpPost("time-sheets")]
    public async Task<ActionResult<ApiResponse<FirmTimeSheetEntryDto>>> CreateTimeSheet(
        [FromBody] CreateTimeSheetEntryDto dto, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        var userId = GetUserId();
        if (tenantId is null || userId is null) return Unauthorized();
        var result = await _governance.CreateTimeSheetAsync(
            tenantId.Value, userId.Value, GetDisplayName(), IsFirmManager(), dto, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<FirmTimeSheetEntryDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<FirmTimeSheetEntryDto>.Ok(result.Value));
    }

    [HttpPut("time-sheets/{id:guid}")]
    public async Task<ActionResult<ApiResponse<FirmTimeSheetEntryDto>>> UpdateTimeSheet(
        Guid id, [FromBody] UpdateTimeSheetEntryDto dto, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        var userId = GetUserId();
        if (tenantId is null || userId is null) return Unauthorized();
        var result = await _governance.UpdateTimeSheetAsync(
            tenantId.Value, userId.Value, IsFirmManager(), id, dto, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<FirmTimeSheetEntryDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<FirmTimeSheetEntryDto>.Ok(result.Value));
    }

    [HttpDelete("time-sheets/{id:guid}")]
    public async Task<IActionResult> DeleteTimeSheet(Guid id, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        var userId = GetUserId();
        if (tenantId is null || userId is null) return Unauthorized();
        var result = await _governance.DeleteTimeSheetAsync(
            tenantId.Value, userId.Value, IsFirmManager(), id, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!, "Feuille de temps supprimée."));
    }

    [HttpPost("time-sheets/{id:guid}/validate")]
    [Authorize(Roles = nameof(UserRole.FirmManager))]
    public async Task<ActionResult<ApiResponse<FirmTimeSheetEntryDto>>> ValidateTimeSheet(
        Guid id, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        var userId = GetUserId();
        if (tenantId is null || userId is null) return Unauthorized();
        var result = await _governance.ValidateTimeSheetAsync(
            tenantId.Value, userId.Value, GetDisplayName(), isManager: true, id, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<FirmTimeSheetEntryDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<FirmTimeSheetEntryDto>.Ok(result.Value, "Feuille de temps validée."));
    }

    [HttpPost("time-sheets/{id:guid}/unvalidate")]
    [Authorize(Roles = nameof(UserRole.FirmManager))]
    public async Task<ActionResult<ApiResponse<FirmTimeSheetEntryDto>>> UnvalidateTimeSheet(
        Guid id, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var result = await _governance.UnvalidateTimeSheetAsync(tenantId.Value, isManager: true, id, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<FirmTimeSheetEntryDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<FirmTimeSheetEntryDto>.Ok(result.Value, "Feuille de temps repassée en brouillon."));
    }

    [HttpPost("time-sheets/{id:guid}/submit")]
    public async Task<ActionResult<ApiResponse<FirmTimeSheetEntryDto>>> SubmitTimeSheet(
        Guid id, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        var userId = GetUserId();
        if (tenantId is null || userId is null) return Unauthorized();
        var result = await _governance.SubmitTimeSheetAsync(
            tenantId.Value, userId.Value, IsFirmManager(), id, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<FirmTimeSheetEntryDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<FirmTimeSheetEntryDto>.Ok(result.Value, "Feuille de temps soumise."));
    }

    [HttpPost("time-sheets/timer/start")]
    public async Task<ActionResult<ApiResponse<FirmTimeSheetEntryDto>>> StartTimeSheetTimer(
        [FromBody] StartTimeSheetTimerDto dto, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        var userId = GetUserId();
        if (tenantId is null || userId is null) return Unauthorized();
        var result = await _governance.StartTimeSheetTimerAsync(
            tenantId.Value, userId.Value, GetDisplayName(), IsFirmManager(), dto, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<FirmTimeSheetEntryDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<FirmTimeSheetEntryDto>.Ok(result.Value, "Timer démarré."));
    }

    [HttpPost("time-sheets/timer/stop")]
    public async Task<ActionResult<ApiResponse<FirmTimeSheetEntryDto>>> StopTimeSheetTimer(
        [FromBody] StopTimeSheetTimerDto dto, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        var userId = GetUserId();
        if (tenantId is null || userId is null) return Unauthorized();
        var result = await _governance.StopTimeSheetTimerAsync(
            tenantId.Value, userId.Value, IsFirmManager(), dto, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<FirmTimeSheetEntryDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<FirmTimeSheetEntryDto>.Ok(result.Value, "Timer arrêté."));
    }

    [HttpPost("time-sheets/duplicate-week")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<FirmTimeSheetEntryDto>>>> DuplicateTimeSheetWeek(
        [FromBody] DuplicateTimeSheetWeekDto dto, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        var userId = GetUserId();
        if (tenantId is null || userId is null) return Unauthorized();
        var result = await _governance.DuplicateTimeSheetWeekAsync(
            tenantId.Value, userId.Value, GetDisplayName(), IsFirmManager(), dto, cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<IReadOnlyList<FirmTimeSheetEntryDto>>.Fail(result.Error.Description));
        return Ok(ApiResponse<IReadOnlyList<FirmTimeSheetEntryDto>>.Ok(
            result.Value, $"{result.Value.Count} ligne(s) dupliquée(s)."));
    }

    /// <summary>
    /// Contrat historique conservé : renvoie le seul nombre de feuilles validées.
    /// Les clients ayant besoin du détail des échecs utilisent <c>validate-bulk/detailed</c>.
    /// </summary>
    [HttpPost("time-sheets/validate-bulk")]
    [Authorize(Roles = nameof(UserRole.FirmManager))]
    public async Task<ActionResult<ApiResponse<int>>> ValidateTimeSheetsBulk(
        [FromBody] ValidateTimeSheetsBulkDto dto, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        var userId = GetUserId();
        if (tenantId is null || userId is null) return Unauthorized();
        var result = await _governance.ValidateTimeSheetsBulkAsync(
            tenantId.Value, userId.Value, GetDisplayName(), isManager: true, dto.Ids, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<int>.Fail(result.Error.Description));
        return Ok(ApiResponse<int>.Ok(result.Value.Validated, $"{result.Value.Validated} feuille(s) validée(s)."));
    }

    [HttpPost("time-sheets/validate-bulk/detailed")]
    [Authorize(Roles = nameof(UserRole.FirmManager))]
    public async Task<ActionResult<ApiResponse<FirmTimeSheetBulkValidationResultDto>>> ValidateTimeSheetsBulkDetailed(
        [FromBody] ValidateTimeSheetsBulkDto dto, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        var userId = GetUserId();
        if (tenantId is null || userId is null) return Unauthorized();
        var result = await _governance.ValidateTimeSheetsBulkAsync(
            tenantId.Value, userId.Value, GetDisplayName(), isManager: true, dto.Ids, cancellationToken);
        if (result.IsFailure)
            return BadRequest(ApiResponse<FirmTimeSheetBulkValidationResultDto>.Fail(result.Error.Description));

        var message = result.Value.Skipped == 0
            ? $"{result.Value.Validated} feuille(s) validée(s)."
            : $"{result.Value.Validated} feuille(s) validée(s), {result.Value.Skipped} ignorée(s).";
        return Ok(ApiResponse<FirmTimeSheetBulkValidationResultDto>.Ok(result.Value, message));
    }

    // ---- Clôture mensuelle ----

    [HttpGet("time-sheets/periods/{year:int}")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<FirmTimeSheetPeriodDto>>>> ListTimeSheetPeriods(
        int year, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var periods = await _governance.ListTimeSheetPeriodsAsync(tenantId.Value, year, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<FirmTimeSheetPeriodDto>>.Ok(periods));
    }

    [HttpPost("time-sheets/periods/{year:int}/{month:int}/lock")]
    [Authorize(Roles = nameof(UserRole.FirmManager))]
    public async Task<ActionResult<ApiResponse<FirmTimeSheetPeriodDto>>> LockTimeSheetPeriod(
        int year, int month, [FromBody] LockTimeSheetPeriodDto dto, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        var userId = GetUserId();
        if (tenantId is null || userId is null) return Unauthorized();
        var result = await _governance.LockTimeSheetPeriodAsync(
            tenantId.Value, userId.Value, GetDisplayName(), isManager: true, year, month, dto.Reason, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<FirmTimeSheetPeriodDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<FirmTimeSheetPeriodDto>.Ok(result.Value, "Période clôturée."));
    }

    [HttpPost("time-sheets/periods/{year:int}/{month:int}/unlock")]
    [Authorize(Roles = nameof(UserRole.FirmManager))]
    public async Task<ActionResult<ApiResponse<FirmTimeSheetPeriodDto>>> UnlockTimeSheetPeriod(
        int year, int month, [FromBody] UnlockTimeSheetPeriodDto dto, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        var userId = GetUserId();
        if (tenantId is null || userId is null) return Unauthorized();
        var result = await _governance.UnlockTimeSheetPeriodAsync(
            tenantId.Value, userId.Value, GetDisplayName(), isManager: true, year, month, dto.Reason, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<FirmTimeSheetPeriodDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<FirmTimeSheetPeriodDto>.Ok(result.Value, "Période rouverte."));
    }

    // ---- Référentiel des codes activité ----

    [HttpGet("activity-codes")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<FirmActivityCodeDto>>>> ListActivityCodes(
        [FromQuery] bool includeInactive = false,
        [FromQuery] bool billableOnly = false,
        CancellationToken cancellationToken = default)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var codes = await _governance.ListActivityCodesAsync(tenantId.Value, includeInactive, billableOnly, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<FirmActivityCodeDto>>.Ok(codes));
    }

    /// <summary>Installe la nomenclature tunisienne par défaut. Sans effet si le cabinet a déjà des codes.</summary>
    [HttpPost("activity-codes/seed-defaults")]
    [Authorize(Roles = nameof(UserRole.FirmManager))]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<FirmActivityCodeDto>>>> SeedDefaultActivityCodes(
        CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var codes = await _governance.SeedDefaultActivityCodesAsync(tenantId.Value, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<FirmActivityCodeDto>>.Ok(codes));
    }

    [HttpPost("activity-codes")]
    [Authorize(Roles = nameof(UserRole.FirmManager))]
    public async Task<ActionResult<ApiResponse<FirmActivityCodeDto>>> CreateActivityCode(
        [FromBody] SaveFirmActivityCodeDto dto, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var result = await _governance.CreateActivityCodeAsync(tenantId.Value, isManager: true, dto, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<FirmActivityCodeDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<FirmActivityCodeDto>.Ok(result.Value, "Code activité créé."));
    }

    [HttpPut("activity-codes/{id:guid}")]
    [Authorize(Roles = nameof(UserRole.FirmManager))]
    public async Task<ActionResult<ApiResponse<FirmActivityCodeDto>>> UpdateActivityCode(
        Guid id, [FromBody] SaveFirmActivityCodeDto dto, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var result = await _governance.UpdateActivityCodeAsync(tenantId.Value, isManager: true, id, dto, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<FirmActivityCodeDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<FirmActivityCodeDto>.Ok(result.Value, "Code activité mis à jour."));
    }

    /// <summary>Désactive le code sans toucher aux saisies qui l'utilisent déjà.</summary>
    [HttpDelete("activity-codes/{id:guid}")]
    [Authorize(Roles = nameof(UserRole.FirmManager))]
    public async Task<ActionResult<ApiResponse<FirmActivityCodeDto>>> DeactivateActivityCode(
        Guid id, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var result = await _governance.SetActivityCodeActiveAsync(
            tenantId.Value, isManager: true, id, isActive: false, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<FirmActivityCodeDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<FirmActivityCodeDto>.Ok(result.Value, "Code activité désactivé."));
    }

    [HttpPost("activity-codes/{id:guid}/activate")]
    [Authorize(Roles = nameof(UserRole.FirmManager))]
    public async Task<ActionResult<ApiResponse<FirmActivityCodeDto>>> ActivateActivityCode(
        Guid id, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var result = await _governance.SetActivityCodeActiveAsync(
            tenantId.Value, isManager: true, id, isActive: true, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<FirmActivityCodeDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<FirmActivityCodeDto>.Ok(result.Value, "Code activité réactivé."));
    }

    // ---- Paramètres d'exercice ----

    [HttpGet("time-sheets/settings/{year:int}")]
    public async Task<ActionResult<ApiResponse<FirmTimeSheetYearSettingsDto>>> GetTimeSheetYearSettings(
        int year, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var settings = await _governance.GetTimeSheetYearSettingsAsync(tenantId.Value, year, cancellationToken);
        return Ok(ApiResponse<FirmTimeSheetYearSettingsDto>.Ok(settings));
    }

    [HttpPut("time-sheets/settings/{year:int}")]
    [Authorize(Roles = nameof(UserRole.FirmManager))]
    public async Task<ActionResult<ApiResponse<FirmTimeSheetYearSettingsDto>>> SaveTimeSheetYearSettings(
        int year, [FromBody] SaveFirmTimeSheetYearSettingsDto dto, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var result = await _governance.SaveTimeSheetYearSettingsAsync(tenantId.Value, year, dto, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<FirmTimeSheetYearSettingsDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<FirmTimeSheetYearSettingsDto>.Ok(result.Value, "Paramètres enregistrés."));
    }

    [HttpGet("expense-notes")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<FirmExpenseNoteDto>>>> ListExpenseNotes(CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var list = await _governance.ListExpenseNotesAsync(tenantId.Value, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<FirmExpenseNoteDto>>.Ok(list));
    }

    [HttpPost("expense-notes")]
    public async Task<ActionResult<ApiResponse<FirmExpenseNoteDto>>> UpsertExpenseNote(
        [FromBody] UpsertFirmExpenseNoteDto dto, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var result = await _governance.UpsertExpenseNoteAsync(tenantId.Value, dto, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<FirmExpenseNoteDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<FirmExpenseNoteDto>.Ok(result.Value));
    }

    [HttpPost("expense-notes/{noteId:guid}/process")]
    public async Task<IActionResult> ProcessExpenseNote(Guid noteId, [FromQuery] bool approve, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var result = await _governance.ProcessExpenseNoteAsync(tenantId.Value, noteId, approve, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!, approve ? "Note approuvée." : "Note rejetée."));
    }

    [HttpPost("expense-notes/{noteId:guid}/submit")]
    public async Task<IActionResult> SubmitExpenseNote(Guid noteId, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var result = await _governance.SubmitExpenseNoteAsync(tenantId.Value, noteId, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!, "Note soumise."));
    }

    [HttpPost("expense-notes/{noteId:guid}/reimburse")]
    public async Task<IActionResult> ReimburseExpenseNote(Guid noteId, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var result = await _governance.MarkExpenseNoteReimbursedAsync(tenantId.Value, noteId, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!, "Note marquée remboursée."));
    }

    [HttpGet("social-overview")]
    public async Task<ActionResult<ApiResponse<FirmSocialOverviewDto>>> GetSocialOverview(CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var dto = await _governance.GetSocialOverviewAsync(tenantId.Value, cancellationToken);
        return Ok(ApiResponse<FirmSocialOverviewDto>.Ok(dto));
    }

    [HttpGet("dossier-assignments")]
    [Authorize(Roles = nameof(UserRole.FirmManager))]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<FirmDossierAssignmentListItemDto>>>> ListDossierAssignments(
        [FromQuery] int assignmentFilter = 1,
        [FromQuery] string? name = null,
        CancellationToken cancellationToken = default)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var list = await _governance.ListDossierAssignmentsAsync(tenantId.Value, assignmentFilter, name, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<FirmDossierAssignmentListItemDto>>.Ok(list));
    }

    [HttpGet("assignable-accountants")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<FirmAssignableAccountantDto>>>> ListAssignableAccountants(
        CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var list = await _governance.ListAssignableAccountantsAsync(tenantId.Value, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<FirmAssignableAccountantDto>>.Ok(list));
    }

    [HttpPost("assign-manager")]
    [Authorize(Roles = nameof(UserRole.FirmManager))]
    public async Task<IActionResult> AssignManager([FromBody] AssignDossierManagerDto dto, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var result = await _governance.AssignDossierManagerAsync(tenantId.Value, userId.Value, dto, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!, "Gestionnaire affecté."));
    }

    [HttpPost("assign-manager-bulk")]
    [Authorize(Roles = nameof(UserRole.FirmManager))]
    public async Task<ActionResult<ApiResponse<AssignDossierManagerBulkResultDto>>> AssignManagerBulk(
        [FromBody] AssignDossierManagerBulkDto dto,
        CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var userId = GetUserId();
        if (userId is null) return Unauthorized();
        var result = await _governance.AssignDossierManagerBulkAsync(tenantId.Value, userId.Value, dto, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<AssignDossierManagerBulkResultDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<AssignDossierManagerBulkResultDto>.Ok(result.Value, "Affectation terminée."));
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

    private Guid? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    private bool IsFirmManager() => User.IsInRole(nameof(UserRole.FirmManager));

    /// <summary>Nom affichable de l'utilisateur courant, replié sur l'e-mail puis sur un libellé générique.</summary>
    private string GetDisplayName()
    {
        var name = $"{User.FindFirstValue(ClaimTypes.GivenName)} {User.FindFirstValue(ClaimTypes.Surname)}".Trim();
        if (!string.IsNullOrWhiteSpace(name))
            return name;
        return User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? "Utilisateur";
    }
}
