using FactuTrust.API.Authorization;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FactuTrust.API.Controllers;

[ApiController]
[Route("api/recurring-contracts")]
[Authorize]
public sealed class RecurringContractsController : ControllerBase
{
    private readonly IRecurringContractService _service;
    private readonly RecurringContractsOptions _options;

    public RecurringContractsController(
        IRecurringContractService service,
        IOptions<RecurringContractsOptions> options)
    {
        _service = service;
        _options = options.Value;
    }

    private ActionResult? GuardEnabled()
    {
        if (!_options.Enabled)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, ApiResponse<object>.Fail(
                "Le module Contrats récurrents est désactivé. Activez Features:RecurringContracts:Enabled."));
        return null;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.RecurringContractsRead)]
    public async Task<ActionResult<ApiResponse<PagedResult<RecurringContractListItemDto>>>> List(
        [FromQuery] string? search,
        [FromQuery] RecurringContractStatus? status,
        [FromQuery] Guid? clientId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (GuardEnabled() is { } guard) return guard;
        var result = await _service.ListAsync(new RecurringContractListQuery
        {
            Search = search,
            Status = status,
            ClientId = clientId,
            Page = page,
            PageSize = pageSize
        }, cancellationToken);
        return Ok(ApiResponse<PagedResult<RecurringContractListItemDto>>.Ok(result));
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.RecurringContractsRead)]
    public async Task<ActionResult<ApiResponse<RecurringContractDto>>> Get(Guid id, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } guard) return guard;
        var dto = await _service.GetAsync(id, cancellationToken);
        if (dto is null) return NotFound(ApiResponse<RecurringContractDto>.Fail("Contrat introuvable"));
        return Ok(ApiResponse<RecurringContractDto>.Ok(dto));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.RecurringContractsCreate)]
    public async Task<ActionResult<ApiResponse<Guid>>> Create(
        [FromBody] UpsertRecurringContractDto dto, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } guard) return guard;
        var result = await _service.CreateAsync(dto, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<Guid>.Fail(result.Error.Description));
        return Ok(ApiResponse<Guid>.Ok(result.Value, "Contrat créé."));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = PermissionPolicies.RecurringContractsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> Update(
        Guid id, [FromBody] UpsertRecurringContractDto dto, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } guard) return guard;
        var result = await _service.UpdateAsync(id, dto, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!, "Contrat mis à jour."));
    }

    [HttpPost("{id:guid}/activate")]
    [Authorize(Policy = PermissionPolicies.RecurringContractsManage)]
    public async Task<ActionResult<ApiResponse<object>>> Activate(Guid id, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } guard) return guard;
        var result = await _service.ActivateAsync(id, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!, "Contrat activé."));
    }

    [HttpPost("{id:guid}/suspend")]
    [Authorize(Policy = PermissionPolicies.RecurringContractsManage)]
    public async Task<ActionResult<ApiResponse<object>>> Suspend(Guid id, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } guard) return guard;
        var result = await _service.SuspendAsync(id, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!, "Contrat suspendu."));
    }

    [HttpPost("{id:guid}/resume")]
    [Authorize(Policy = PermissionPolicies.RecurringContractsManage)]
    public async Task<ActionResult<ApiResponse<object>>> Resume(Guid id, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } guard) return guard;
        var result = await _service.ResumeAsync(id, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!, "Contrat repris."));
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = PermissionPolicies.RecurringContractsManage)]
    public async Task<ActionResult<ApiResponse<object>>> Cancel(
        Guid id, [FromQuery] DateTime? cancellationDate, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } guard) return guard;
        var result = await _service.CancelAsync(id, cancellationDate, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!, "Contrat résilié."));
    }

    [HttpPost("{id:guid}/amend")]
    [Authorize(Policy = PermissionPolicies.RecurringContractsManage)]
    public async Task<ActionResult<ApiResponse<object>>> Amend(
        Guid id, [FromBody] AmendRecurringContractDto dto, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } guard) return guard;
        var result = await _service.AmendAsync(id, dto, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!, "Avenant enregistré."));
    }

    [HttpPost("{id:guid}/clone")]
    [Authorize(Policy = PermissionPolicies.RecurringContractsCreate)]
    public async Task<ActionResult<ApiResponse<Guid>>> Clone(
        Guid id, [FromBody] CloneRecurringContractDto? dto, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } guard) return guard;
        var result = await _service.CloneAsync(id, dto ?? new CloneRecurringContractDto(), cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<Guid>.Fail(result.Error.Description));
        return Ok(ApiResponse<Guid>.Ok(result.Value, "Contrat cloné en brouillon."));
    }

    [HttpPost("{id:guid}/renew")]
    [Authorize(Policy = PermissionPolicies.RecurringContractsManage)]
    public async Task<ActionResult<ApiResponse<object>>> Renew(
        Guid id, [FromBody] RenewRecurringContractDto? dto, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } guard) return guard;
        var result = await _service.RenewAsync(id, dto ?? new RenewRecurringContractDto(), cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(new { newEndDate = result.Value },
            $"Contrat renouvelé jusqu'au {result.Value:dd/MM/yyyy}."));
    }

    [HttpPatch("{id:guid}/notes")]
    [Authorize(Policy = PermissionPolicies.RecurringContractsUpdate)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateNotes(
        Guid id, [FromBody] UpdateRecurringContractNotesDto dto, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } guard) return guard;
        var result = await _service.UpdateNotesAsync(id, dto.Notes, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!, "Notes mises à jour."));
    }

    [HttpGet("{id:guid}/amendments")]
    [Authorize(Policy = PermissionPolicies.RecurringContractsRead)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<RecurringContractAmendmentDto>>>> ListAmendments(
        Guid id, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } guard) return guard;
        var items = await _service.ListAmendmentsAsync(id, cancellationToken);
        if (items is null) return NotFound(ApiResponse<object>.Fail("Contrat introuvable"));
        return Ok(ApiResponse<IReadOnlyList<RecurringContractAmendmentDto>>.Ok(items));
    }

    [HttpGet("{id:guid}/amendments/{amendmentId:guid}")]
    [Authorize(Policy = PermissionPolicies.RecurringContractsRead)]
    public async Task<ActionResult<ApiResponse<RecurringContractAmendmentDetailDto>>> GetAmendment(
        Guid id, Guid amendmentId, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } guard) return guard;
        var dto = await _service.GetAmendmentAsync(id, amendmentId, cancellationToken);
        if (dto is null) return NotFound(ApiResponse<RecurringContractAmendmentDetailDto>.Fail("Avenant introuvable"));
        return Ok(ApiResponse<RecurringContractAmendmentDetailDto>.Ok(dto));
    }

    [HttpGet("{id:guid}/schedule")]
    [Authorize(Policy = PermissionPolicies.RecurringContractsRead)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<RecurringContractScheduleItemDto>>>> GetSchedule(
        Guid id, [FromQuery] int count = 12, CancellationToken cancellationToken = default)
    {
        if (GuardEnabled() is { } guard) return guard;
        var items = await _service.GetScheduleAsync(id, count, cancellationToken);
        if (items is null) return NotFound(ApiResponse<object>.Fail("Contrat introuvable"));
        return Ok(ApiResponse<IReadOnlyList<RecurringContractScheduleItemDto>>.Ok(items));
    }

    [HttpGet("{id:guid}/financial-summary")]
    [Authorize(Policy = PermissionPolicies.RecurringContractsRead)]
    public async Task<ActionResult<ApiResponse<RecurringContractFinancialSummaryDto>>> GetFinancialSummary(
        Guid id, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } guard) return guard;
        var dto = await _service.GetFinancialSummaryAsync(id, cancellationToken);
        if (dto is null) return NotFound(ApiResponse<object>.Fail("Contrat introuvable"));
        return Ok(ApiResponse<RecurringContractFinancialSummaryDto>.Ok(dto));
    }

    [HttpGet("{id:guid}/linked-invoices")]
    [Authorize(Policy = PermissionPolicies.RecurringContractsRead)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<RecurringContractLinkedInvoiceDto>>>> GetLinkedInvoices(
        Guid id, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } guard) return guard;
        var items = await _service.GetLinkedInvoicesAsync(id, cancellationToken);
        if (items is null) return NotFound(ApiResponse<object>.Fail("Contrat introuvable"));
        return Ok(ApiResponse<IReadOnlyList<RecurringContractLinkedInvoiceDto>>.Ok(items));
    }

    [HttpGet("{id:guid}/evolution")]
    [Authorize(Policy = PermissionPolicies.RecurringContractsRead)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<RecurringContractEvolutionPointDto>>>> GetEvolution(
        Guid id, [FromQuery] int months = 6, CancellationToken cancellationToken = default)
    {
        if (GuardEnabled() is { } guard) return guard;
        var items = await _service.GetEvolutionAsync(id, months, cancellationToken);
        if (items is null) return NotFound(ApiResponse<object>.Fail("Contrat introuvable"));
        return Ok(ApiResponse<IReadOnlyList<RecurringContractEvolutionPointDto>>.Ok(items));
    }

    [HttpGet("{id:guid}/detail")]
    [Authorize(Policy = PermissionPolicies.RecurringContractsRead)]
    public async Task<ActionResult<ApiResponse<RecurringContractDetailDto>>> GetDetail(
        Guid id, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } guard) return guard;
        var dto = await _service.GetDetailAsync(id, cancellationToken);
        if (dto is null) return NotFound(ApiResponse<object>.Fail("Contrat introuvable"));
        return Ok(ApiResponse<RecurringContractDetailDto>.Ok(dto));
    }

    [HttpGet("stats")]
    [Authorize(Policy = PermissionPolicies.RecurringContractsRead)]
    public async Task<ActionResult<ApiResponse<RecurringContractStatsDto>>> GetStats(
        CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } guard) return guard;
        var stats = await _service.GetStatsAsync(cancellationToken);
        return Ok(ApiResponse<RecurringContractStatsDto>.Ok(stats));
    }

    [HttpPost("convert-from-quote/{quoteId:guid}")]
    [Authorize(Policy = PermissionPolicies.RecurringContractsCreate)]
    public async Task<ActionResult<ApiResponse<Guid>>> ConvertFromQuote(
        Guid quoteId, [FromBody] UpsertRecurringContractDto dto, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } guard) return guard;
        var result = await _service.ConvertFromQuoteAsync(quoteId, dto, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<Guid>.Fail(result.Error.Description));
        return Ok(ApiResponse<Guid>.Ok(result.Value, "Contrat créé depuis le devis."));
    }

    [HttpGet("usage-metrics")]
    [Authorize(Policy = PermissionPolicies.RecurringContractsRead)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<UsageMetricDto>>>> ListUsageMetrics(
        CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } guard) return guard;
        var items = await _service.ListUsageMetricsAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<UsageMetricDto>>.Ok(items));
    }

    [HttpPost("usage-metrics")]
    [Authorize(Policy = PermissionPolicies.RecurringContractsManage)]
    public async Task<ActionResult<ApiResponse<Guid>>> CreateUsageMetric(
        [FromBody] UpsertUsageMetricDto dto, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } guard) return guard;
        var result = await _service.CreateUsageMetricAsync(dto, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<Guid>.Fail(result.Error.Description));
        return Ok(ApiResponse<Guid>.Ok(result.Value));
    }

    [HttpPut("usage-metrics/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.RecurringContractsManage)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateUsageMetric(
        Guid id, [FromBody] UpsertUsageMetricDto dto, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } guard) return guard;
        var result = await _service.UpdateUsageMetricAsync(id, dto, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        return Ok(ApiResponse<object>.Ok(null!));
    }

    [HttpGet("{id:guid}/usage-records")]
    [Authorize(Policy = PermissionPolicies.RecurringContractsRead)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<UsageRecordDto>>>> ListUsageRecords(
        Guid id, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } guard) return guard;
        var items = await _service.ListUsageRecordsAsync(id, from, to, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<UsageRecordDto>>.Ok(items));
    }

    [HttpPost("{id:guid}/usage-records")]
    [Authorize(Policy = PermissionPolicies.RecurringContractsRecordUsage)]
    public async Task<ActionResult<ApiResponse<Guid>>> RecordUsage(
        Guid id, [FromBody] RecordUsageDto dto, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } guard) return guard;
        var result = await _service.RecordUsageAsync(id, dto, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<Guid>.Fail(result.Error.Description));
        return Ok(ApiResponse<Guid>.Ok(result.Value));
    }

    [HttpPost("{id:guid}/usage-records/import")]
    [Authorize(Policy = PermissionPolicies.RecurringContractsRecordUsage)]
    public async Task<ActionResult<ApiResponse<int>>> ImportUsageRecords(
        Guid id, [FromBody] IReadOnlyList<ImportUsageRecordRowDto> rows, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } guard) return guard;
        var result = await _service.ImportUsageRecordsAsync(id, rows, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<int>.Fail(result.Error.Description));
        return Ok(ApiResponse<int>.Ok(result.Value, $"{result.Value} ligne(s) importée(s)."));
    }

    [HttpGet("{id:guid}/billing-runs")]
    [Authorize(Policy = PermissionPolicies.RecurringContractsRead)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<RecurringContractBillingRunDto>>>> ListBillingRuns(
        Guid id, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } guard) return guard;
        var items = await _service.ListBillingRunsAsync(id, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<RecurringContractBillingRunDto>>.Ok(items));
    }

    [HttpGet("pending-drafts")]
    [Authorize(Policy = PermissionPolicies.RecurringContractsRead)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<PendingRecurringDraftDto>>>> ListPendingDrafts(
        CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } guard) return guard;
        var items = await _service.ListPendingDraftsAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<PendingRecurringDraftDto>>.Ok(items));
    }

    [HttpPost("billing-runs/trigger")]
    [Authorize(Policy = PermissionPolicies.RecurringContractsTriggerBilling)]
    public async Task<ActionResult<ApiResponse<int>>> TriggerBilling(
        [FromQuery] Guid? contractId, CancellationToken cancellationToken)
    {
        if (GuardEnabled() is { } guard) return guard;
        var result = await _service.TriggerBillingAsync(contractId, cancellationToken);
        if (result.IsFailure) return BadRequest(ApiResponse<int>.Fail(result.Error.Description));
        return Ok(ApiResponse<int>.Ok(result.Value, $"{result.Value} brouillon(s) généré(s)."));
    }
}
