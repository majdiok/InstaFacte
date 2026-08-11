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
public sealed class FirmLeavesController : ControllerBase
{
    private readonly IFirmLeaveService _leaves;
    private readonly IFirmGovernanceFeature _feature;

    public FirmLeavesController(IFirmLeaveService leaves, IFirmGovernanceFeature feature)
    {
        _leaves = leaves;
        _feature = feature;
    }

    [HttpGet("leaves/overview")]
    public async Task<ActionResult<ApiResponse<FirmLeaveOverviewDto>>> GetOverview([FromQuery] int? year, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        var userId = GetUserId();
        if (tenantId is null || userId is null) return Unauthorized();
        var y = year ?? DateTime.UtcNow.Year;
        var dto = await _leaves.GetOverviewAsync(tenantId.Value, y, IsFirmManager(), userId.Value, cancellationToken);
        return Ok(ApiResponse<FirmLeaveOverviewDto>.Ok(dto));
    }

    [HttpGet("leaves/calendar")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<FirmLeaveCalendarEntryDto>>>> GetCalendar(
        [FromQuery] DateTime from, [FromQuery] DateTime to, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        var userId = GetUserId();
        if (tenantId is null || userId is null) return Unauthorized();
        var list = await _leaves.GetCalendarAsync(tenantId.Value, from, to, IsFirmManager(), userId.Value, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<FirmLeaveCalendarEntryDto>>.Ok(list));
    }

    [HttpGet("leaves")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<FirmLeaveRequestDto>>>> List(
        [FromQuery] Guid? userId,
        [FromQuery] int? status,
        [FromQuery] Guid? typeId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? year,
        CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        var actorId = GetUserId();
        if (tenantId is null || actorId is null) return Unauthorized();
        var list = await _leaves.ListRequestsAsync(
            tenantId.Value, IsFirmManager(), actorId.Value, userId, status, typeId, from, to, year, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<FirmLeaveRequestDto>>.Ok(list));
    }

    [HttpGet("leaves/{id:guid}")]
    public async Task<ActionResult<ApiResponse<FirmLeaveRequestDto>>> Get(Guid id, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        var actorId = GetUserId();
        if (tenantId is null || actorId is null) return Unauthorized();
        var dto = await _leaves.GetRequestAsync(tenantId.Value, id, IsFirmManager(), actorId.Value, cancellationToken);
        if (dto is null) return NotFound(ApiResponse<FirmLeaveRequestDto>.Fail("Demande introuvable."));
        return Ok(ApiResponse<FirmLeaveRequestDto>.Ok(dto));
    }

    [HttpPost("leaves/compute-days")]
    public async Task<ActionResult<ApiResponse<ComputeFirmLeaveDaysResultDto>>> ComputeDays(
        [FromBody] ComputeFirmLeaveDaysDto dto, [FromQuery] int? year, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var result = await _leaves.ComputeDaysAsync(tenantId.Value, year ?? dto.StartDate.Year, dto, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<ComputeFirmLeaveDaysResultDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<ComputeFirmLeaveDaysResultDto>.Ok(result.Value));
    }

    [HttpPost("leaves")]
    public async Task<ActionResult<ApiResponse<FirmLeaveRequestDto>>> Create(
        [FromBody] CreateFirmLeaveRequestDto dto, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        var actorId = GetUserId();
        if (tenantId is null || actorId is null) return Unauthorized();
        var result = await _leaves.CreateAsync(tenantId.Value, actorId.Value, IsFirmManager(), dto, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<FirmLeaveRequestDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<FirmLeaveRequestDto>.Ok(result.Value));
    }

    [HttpPut("leaves/{id:guid}")]
    public async Task<ActionResult<ApiResponse<FirmLeaveRequestDto>>> Update(
        Guid id, [FromBody] UpdateFirmLeaveRequestDto dto, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        var actorId = GetUserId();
        if (tenantId is null || actorId is null) return Unauthorized();
        var result = await _leaves.UpdateAsync(tenantId.Value, actorId.Value, IsFirmManager(), id, dto, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<FirmLeaveRequestDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<FirmLeaveRequestDto>.Ok(result.Value));
    }

    [HttpPost("leaves/{id:guid}/submit")]
    public async Task<ActionResult<ApiResponse<FirmLeaveRequestDto>>> Submit(Guid id, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        var actorId = GetUserId();
        if (tenantId is null || actorId is null) return Unauthorized();
        var result = await _leaves.SubmitAsync(tenantId.Value, actorId.Value, IsFirmManager(), id, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<FirmLeaveRequestDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<FirmLeaveRequestDto>.Ok(result.Value, "Demande soumise."));
    }

    [HttpPost("leaves/{id:guid}/cancel")]
    public async Task<ActionResult<ApiResponse<FirmLeaveRequestDto>>> Cancel(Guid id, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        var actorId = GetUserId();
        if (tenantId is null || actorId is null) return Unauthorized();
        var result = await _leaves.CancelAsync(tenantId.Value, actorId.Value, IsFirmManager(), id, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<FirmLeaveRequestDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<FirmLeaveRequestDto>.Ok(result.Value, "Demande annulée."));
    }

    [HttpPost("leaves/{id:guid}/process")]
    [Authorize(Roles = nameof(UserRole.FirmManager))]
    public async Task<ActionResult<ApiResponse<FirmLeaveRequestDto>>> Process(
        Guid id, [FromBody] ProcessFirmLeaveDto dto, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        var actorId = GetUserId();
        if (tenantId is null || actorId is null) return Unauthorized();
        var result = await _leaves.ProcessAsync(tenantId.Value, actorId.Value, GetDisplayName(), id, dto, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<FirmLeaveRequestDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<FirmLeaveRequestDto>.Ok(result.Value, dto.Approve ? "Demande acceptée." : "Demande refusée."));
    }

    [HttpGet("leaves/balances")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<FirmLeaveBalanceDto>>>> ListBalances(
        [FromQuery] int? year, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        var actorId = GetUserId();
        if (tenantId is null || actorId is null) return Unauthorized();
        var list = await _leaves.ListBalancesAsync(tenantId.Value, year ?? DateTime.UtcNow.Year, IsFirmManager(), actorId.Value, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<FirmLeaveBalanceDto>>.Ok(list));
    }

    [HttpGet("leaves/balances/me")]
    public async Task<ActionResult<ApiResponse<FirmLeaveBalanceDto>>> GetMyBalance(
        [FromQuery] int? year, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        var actorId = GetUserId();
        if (tenantId is null || actorId is null) return Unauthorized();
        var dto = await _leaves.GetMyBalanceAsync(tenantId.Value, actorId.Value, year ?? DateTime.UtcNow.Year, cancellationToken);
        if (dto is null) return NotFound(ApiResponse<FirmLeaveBalanceDto>.Fail("Solde introuvable."));
        return Ok(ApiResponse<FirmLeaveBalanceDto>.Ok(dto));
    }

    [HttpPut("leaves/balances/{userId:guid}")]
    [Authorize(Roles = nameof(UserRole.FirmManager))]
    public async Task<ActionResult<ApiResponse<FirmLeaveBalanceDto>>> SetBalance(
        Guid userId, [FromQuery] int? year, [FromBody] SetFirmLeaveBalanceDto dto, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var result = await _leaves.SetBalanceAsync(tenantId.Value, userId, year ?? DateTime.UtcNow.Year, dto, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<FirmLeaveBalanceDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<FirmLeaveBalanceDto>.Ok(result.Value));
    }

    [HttpGet("leaves/types")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<FirmLeaveTypeDto>>>> ListTypes(
        [FromQuery] bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var list = await _leaves.ListTypesAsync(tenantId.Value, activeOnly, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<FirmLeaveTypeDto>>.Ok(list));
    }

    [HttpPost("leaves/types")]
    [Authorize(Roles = nameof(UserRole.FirmManager))]
    public async Task<ActionResult<ApiResponse<FirmLeaveTypeDto>>> UpsertType(
        [FromBody] UpsertFirmLeaveTypeDto dto, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var result = await _leaves.UpsertTypeAsync(tenantId.Value, dto, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<FirmLeaveTypeDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<FirmLeaveTypeDto>.Ok(result.Value));
    }

    [HttpGet("leaves/settings/{year:int}")]
    public async Task<ActionResult<ApiResponse<FirmLeaveSettingsDto>>> GetSettings(int year, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var dto = await _leaves.GetSettingsAsync(tenantId.Value, year, cancellationToken);
        return Ok(ApiResponse<FirmLeaveSettingsDto>.Ok(dto));
    }

    [HttpPut("leaves/settings/{year:int}")]
    [Authorize(Roles = nameof(UserRole.FirmManager))]
    public async Task<ActionResult<ApiResponse<FirmLeaveSettingsDto>>> UpdateSettings(
        int year, [FromBody] UpdateFirmLeaveSettingsDto dto, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var result = await _leaves.UpdateSettingsAsync(tenantId.Value, year, dto, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<FirmLeaveSettingsDto>.Fail(result.Error.Description));
        return Ok(ApiResponse<FirmLeaveSettingsDto>.Ok(result.Value));
    }

    [HttpGet("leaves/export")]
    [Authorize(Roles = nameof(UserRole.FirmManager))]
    public async Task<IActionResult> ExportList(
        [FromQuery] int? year, [FromQuery] int? status, [FromQuery] Guid? typeId, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var result = await _leaves.ExportListAsync(tenantId.Value, year, status, typeId, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return File(result.Value.Content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", result.Value.FileName);
    }

    /// <summary>Écarts entre congés approuvés et report en paie interne.</summary>
    [HttpGet("leaves/reconciliation")]
    [Authorize(Roles = nameof(UserRole.FirmManager))]
    public async Task<IActionResult> GetReconciliation(
        [FromQuery] int? year, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var data = await _leaves.GetReconciliationAsync(
            tenantId.Value, year ?? DateTime.UtcNow.Year, cancellationToken);
        return Ok(ApiResponse<FirmLeaveReconciliationDto>.Ok(data));
    }

    /// <summary>Rejoue le report vers la paie d'une demande, ou de toutes celles en écart.</summary>
    [HttpPost("leaves/reconciliation/replay")]
    [Authorize(Roles = nameof(UserRole.FirmManager))]
    public async Task<IActionResult> ReplayPayrollMirror(
        [FromQuery] int? year, [FromQuery] Guid? leaveRequestId, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var result = await _leaves.ReplayPayrollMirrorAsync(
            tenantId.Value, year ?? DateTime.UtcNow.Year, leaveRequestId, cancellationToken);
        return result.IsFailure
            ? BadRequest(ApiResponse<object>.Fail(result.Error.Description))
            : Ok(ApiResponse<FirmLeaveReplayResultDto>.Ok(result.Value));
    }

    [HttpGet("leaves/export/synthesis")]
    [Authorize(Roles = nameof(UserRole.FirmManager))]
    public async Task<IActionResult> ExportSynthesis([FromQuery] int? year, CancellationToken cancellationToken)
    {
        if (!EnsureEnabled(out var disabled)) return disabled!;
        var tenantId = GetHomeTenantId();
        if (tenantId is null) return Unauthorized();
        var result = await _leaves.ExportSynthesisAsync(tenantId.Value, year ?? DateTime.UtcNow.Year, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return File(result.Value.Content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", result.Value.FileName);
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

    private string GetDisplayName()
    {
        var name = $"{User.FindFirstValue(ClaimTypes.GivenName)} {User.FindFirstValue(ClaimTypes.Surname)}".Trim();
        if (!string.IsNullOrWhiteSpace(name)) return name;
        return User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? "Utilisateur";
    }
}
