using System.Security.Claims;
using FactuTrust.API.Authorization;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

[ApiController]
[Route("api/firm/fiscal-schedule")]
[Authorize(Roles = $"{nameof(UserRole.FirmManager)},{nameof(UserRole.FirmAccountant)}")]
[Filters.RequireAccountingFirmsFeature]
public sealed class FirmFiscalScheduleController : ControllerBase
{
    private const long MaxUploadBytes = 10 * 1024 * 1024;

    private readonly IFirmFiscalScheduleService _readService;
    private readonly IFirmFiscalScheduleWriteService _writeService;

    public FirmFiscalScheduleController(
        IFirmFiscalScheduleService readService,
        IFirmFiscalScheduleWriteService writeService)
    {
        _readService = readService;
        _writeService = writeService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<FiscalScheduleListDto>>> GetSchedule(
        [FromQuery] FiscalScheduleFiltersDto filters,
        CancellationToken cancellationToken)
    {
        var tenantId = GetHomeTenantId();
        if (tenantId is null)
            return Unauthorized();

        var schedule = await _readService.GetScheduleAsync(tenantId.Value, filters, cancellationToken);
        return Ok(ApiResponse<FiscalScheduleListDto>.Ok(schedule));
    }

    [HttpGet("companies")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<FirmClientDossierDto>>>> GetCompanies(CancellationToken cancellationToken)
    {
        var tenantId = GetHomeTenantId();
        if (tenantId is null)
            return Unauthorized();

        var companies = await _writeService.GetCompaniesAsync(tenantId.Value, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<FirmClientDossierDto>>.Ok(companies));
    }

    [HttpPost("{companyTenantId:guid}/ensure/{fiscalYear:int}")]
    [Authorize(Policy = PermissionPolicies.AccountingCreate)]
    public async Task<ActionResult<ApiResponse<int>>> EnsureFiscalYear(Guid companyTenantId, int fiscalYear, CancellationToken cancellationToken)
    {
        var tenantId = GetHomeTenantId();
        if (tenantId is null)
            return Unauthorized();

        var result = await _writeService.EnsureFiscalYearAsync(tenantId.Value, companyTenantId, fiscalYear, cancellationToken);
        return ToActionResult(result, value => Ok(ApiResponse<int>.Ok(value, $"{value} echeance(s) generee(s).")));
    }

    [HttpPost("{companyTenantId:guid}/entries")]
    [Authorize(Policy = PermissionPolicies.AccountingCreate)]
    public async Task<ActionResult<ApiResponse<FiscalScheduleEntryDto>>> CreateEntry(
        Guid companyTenantId,
        [FromBody] CreateFiscalScheduleEntryRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = GetHomeTenantId();
        if (tenantId is null)
            return Unauthorized();

        var result = await _writeService.CreateEntryAsync(tenantId.Value, companyTenantId, request, cancellationToken);
        return ToActionResult(result, value => Ok(ApiResponse<FiscalScheduleEntryDto>.Ok(value, "Echeance fiscale creee.")));
    }

    [HttpPut("{companyTenantId:guid}/entries/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.AccountingCreate)]
    public async Task<ActionResult<ApiResponse<FiscalScheduleEntryDto>>> UpdateEntry(
        Guid companyTenantId,
        Guid id,
        [FromBody] UpdateFiscalScheduleEntryRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = GetHomeTenantId();
        if (tenantId is null)
            return Unauthorized();

        var result = await _writeService.UpdateEntryAsync(tenantId.Value, companyTenantId, id, request, cancellationToken);
        return ToActionResult(result, value => Ok(ApiResponse<FiscalScheduleEntryDto>.Ok(value, "Echeance fiscale mise a jour.")));
    }

    [HttpDelete("{companyTenantId:guid}/entries/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.AccountingCreate)]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteEntry(Guid companyTenantId, Guid id, CancellationToken cancellationToken)
    {
        var tenantId = GetHomeTenantId();
        if (tenantId is null)
            return Unauthorized();

        var result = await _writeService.DeleteEntryAsync(tenantId.Value, companyTenantId, id, cancellationToken);
        if (result.IsFailure)
            return MapFailure(result);
        return Ok(ApiResponse<bool>.Ok(true, "Echeance annulee."));
    }

    [HttpPost("{companyTenantId:guid}/entries/{id:guid}/deposit")]
    [Authorize(Policy = PermissionPolicies.AccountingCreate)]
    public async Task<ActionResult<ApiResponse<FiscalScheduleEntryDto>>> MarkDeposited(
        Guid companyTenantId,
        Guid id,
        [FromBody] MarkFiscalScheduleDepositedRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = GetHomeTenantId();
        if (tenantId is null)
            return Unauthorized();

        var result = await _writeService.MarkDepositedAsync(tenantId.Value, companyTenantId, id, request, cancellationToken);
        return ToActionResult(result, value => Ok(ApiResponse<FiscalScheduleEntryDto>.Ok(value, "Echeance marquee comme deposee.")));
    }

    [HttpPost("{companyTenantId:guid}/entries/{id:guid}/payment")]
    [Authorize(Policy = PermissionPolicies.AccountingCreate)]
    public async Task<ActionResult<ApiResponse<FiscalScheduleEntryDto>>> CapturePayment(
        Guid companyTenantId,
        Guid id,
        [FromBody] CaptureFiscalSchedulePaymentRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = GetHomeTenantId();
        if (tenantId is null)
            return Unauthorized();

        var result = await _writeService.CapturePaymentAsync(tenantId.Value, companyTenantId, id, request, cancellationToken);
        return ToActionResult(result, value => Ok(ApiResponse<FiscalScheduleEntryDto>.Ok(value, "Paiement saisi.")));
    }

    [HttpPost("{companyTenantId:guid}/entries/{id:guid}/validate")]
    [Authorize(Policy = PermissionPolicies.AccountingCreate)]
    public async Task<ActionResult<ApiResponse<FiscalScheduleEntryDto>>> MarkValidated(
        Guid companyTenantId,
        Guid id,
        [FromBody] MarkFiscalScheduleValidatedRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = GetHomeTenantId();
        if (tenantId is null)
            return Unauthorized();

        var result = await _writeService.MarkValidatedAsync(tenantId.Value, companyTenantId, id, request, cancellationToken);
        return ToActionResult(result, value => Ok(ApiResponse<FiscalScheduleEntryDto>.Ok(value, "Echeance marquee comme validee.")));
    }

    [HttpPost("{companyTenantId:guid}/entries/{id:guid}/reminder")]
    [Authorize(Policy = PermissionPolicies.AccountingCreate)]
    public async Task<ActionResult<ApiResponse<FiscalScheduleEntryDto>>> ScheduleReminder(
        Guid companyTenantId,
        Guid id,
        [FromBody] ScheduleFiscalReminderRequest request,
        CancellationToken cancellationToken)
    {
        var tenantId = GetHomeTenantId();
        if (tenantId is null)
            return Unauthorized();

        var result = await _writeService.ScheduleReminderAsync(tenantId.Value, companyTenantId, id, request, cancellationToken);
        return ToActionResult(result, value => Ok(ApiResponse<FiscalScheduleEntryDto>.Ok(value, "Rappel fiscal planifie.")));
    }

    [HttpGet("{companyTenantId:guid}/entries/{id:guid}/history")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<FiscalScheduleHistoryDto>>>> GetHistory(
        Guid companyTenantId,
        Guid id,
        CancellationToken cancellationToken)
    {
        var tenantId = GetHomeTenantId();
        if (tenantId is null)
            return Unauthorized();

        var result = await _writeService.GetHistoryAsync(tenantId.Value, companyTenantId, id, cancellationToken);
        return ToActionResult(result, value => Ok(ApiResponse<IReadOnlyList<FiscalScheduleHistoryDto>>.Ok(value)));
    }

    [HttpGet("{companyTenantId:guid}/entries/{id:guid}/attachments")]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<FiscalScheduleAttachmentDto>>>> GetAttachments(
        Guid companyTenantId,
        Guid id,
        CancellationToken cancellationToken)
    {
        var tenantId = GetHomeTenantId();
        if (tenantId is null)
            return Unauthorized();

        var result = await _writeService.GetAttachmentsAsync(tenantId.Value, companyTenantId, id, cancellationToken);
        return ToActionResult(result, value => Ok(ApiResponse<IReadOnlyList<FiscalScheduleAttachmentDto>>.Ok(value)));
    }

    [HttpPost("{companyTenantId:guid}/entries/{id:guid}/attachments")]
    [Authorize(Policy = PermissionPolicies.AccountingCreate)]
    [RequestSizeLimit(MaxUploadBytes + 512_000)]
    public async Task<ActionResult<ApiResponse<FiscalScheduleAttachmentDto>>> UploadAttachment(
        Guid companyTenantId,
        Guid id,
        [FromForm] IFormFile file,
        CancellationToken cancellationToken)
    {
        var tenantId = GetHomeTenantId();
        if (tenantId is null)
            return Unauthorized();
        if (file is null || file.Length == 0)
            return BadRequest(ApiResponse<object>.Fail("Fichier requis."));

        await using var stream = file.OpenReadStream();
        var result = await _writeService.UploadAttachmentAsync(
            tenantId.Value,
            companyTenantId,
            id,
            file.FileName,
            file.ContentType,
            stream,
            cancellationToken);
        return ToActionResult(result, value => Ok(ApiResponse<FiscalScheduleAttachmentDto>.Ok(value, "Piece jointe ajoutee.")));
    }

    [HttpGet("{companyTenantId:guid}/entries/{id:guid}/attachments/{attachmentId:guid}/download")]
    public async Task<IActionResult> DownloadAttachment(
        Guid companyTenantId,
        Guid id,
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        var tenantId = GetHomeTenantId();
        if (tenantId is null)
            return Unauthorized();

        var result = await _writeService.DownloadAttachmentAsync(tenantId.Value, companyTenantId, id, attachmentId, cancellationToken);
        if (result.IsFailure)
            return MapFailure(result);
        return File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
    }

    [HttpDelete("{companyTenantId:guid}/entries/{id:guid}/attachments/{attachmentId:guid}")]
    [Authorize(Policy = PermissionPolicies.AccountingCreate)]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteAttachment(
        Guid companyTenantId,
        Guid id,
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        var tenantId = GetHomeTenantId();
        if (tenantId is null)
            return Unauthorized();

        var result = await _writeService.DeleteAttachmentAsync(tenantId.Value, companyTenantId, id, attachmentId, cancellationToken);
        if (result.IsFailure)
            return MapFailure(result);
        return Ok(ApiResponse<bool>.Ok(true, "Piece jointe supprimee."));
    }

    private Guid? GetHomeTenantId()
    {
        var claim = User.FindFirstValue("tenant_id");
        return Guid.TryParse(claim, out var id) ? id : null;
    }

    private ActionResult ToActionResult<T>(Domain.Common.Result<T> result, Func<T, ActionResult> onSuccess)
    {
        if (result.IsFailure)
            return MapFailure(result);
        return onSuccess(result.Value);
    }

    private ActionResult MapFailure<T>(Domain.Common.Result<T> result) => MapFailure((Domain.Common.Result)result!);

    private ActionResult MapFailure(Domain.Common.Result result)
    {
        if (string.Equals(result.Error.Code, "Forbidden", StringComparison.Ordinal))
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail(result.Error.Description));
        if (string.Equals(result.Error.Code, "NotFound", StringComparison.Ordinal) || result.Error.Code.StartsWith("NotFound.", StringComparison.Ordinal))
            return NotFound(ApiResponse<object>.Fail(result.Error.Description));
        if (string.Equals(result.Error.Code, "Conflict", StringComparison.Ordinal))
            return Conflict(ApiResponse<object>.Fail(result.Error.Description));
        return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
    }
}
