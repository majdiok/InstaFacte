using FactuTrust.API.Authorization;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Payroll.Regularization;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>
/// Régularisation IRPP/CSS annuelle (module RH &amp; Paie) : génération sur un cycle calculé,
/// prévisualisation, ajustement manuel et consultation.
/// </summary>
[ApiController]
[Route("api/payroll")]
[Authorize]
public class PayrollRegularizationsController : ControllerBase
{
    private readonly IMediator _mediator;

    public PayrollRegularizationsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Régularisations enregistrées pour un mois de paie.</summary>
    [HttpGet("regularizations")]
    [Authorize(Policy = PermissionPolicies.PayrollRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<IrppRegularizationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] int year, [FromQuery] int month, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ListIrppRegularizationsForMonthQuery(year, month), cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<IrppRegularizationDto>>.Ok(result));
    }

    /// <summary>Calcule une régularisation sans rien persister (bouton « Calculer »).</summary>
    [HttpGet("regularizations/preview")]
    [Authorize(Policy = PermissionPolicies.PayrollRead)]
    [ProducesResponseType(typeof(ApiResponse<IrppRegularizationPreviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Preview(
        [FromQuery] Guid employeeId,
        [FromQuery] int year,
        [FromQuery] int month,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new PreviewIrppRegularizationQuery(employeeId, year, month), cancellationToken);
        if (result.IsFailure)
        {
            if (result.Error.Code.Contains("NotFound"))
                return NotFound(ApiResponse<IrppRegularizationPreviewDto>.Fail(result.Error.Description));
            return BadRequest(ApiResponse<IrppRegularizationPreviewDto>.Fail(result.Error.Description));
        }
        return Ok(ApiResponse<IrppRegularizationPreviewDto>.Ok(result.Value));
    }

    /// <summary>
    /// Génère les régularisations de tous les salariés éligibles d'un cycle calculé.
    /// Idempotent : relancer produit les mêmes montants et préserve les ajustements manuels.
    /// </summary>
    [HttpPost("runs/{runId:guid}/regularizations/generate")]
    [Authorize(Policy = PermissionPolicies.PayrollRun)]
    [Authorize(Policy = PermissionPolicies.PayrollFirmOperation)]
    [ProducesResponseType(typeof(ApiResponse<GenerateIrppRegularizationsResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Generate(Guid runId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GenerateIrppRegularizationsCommand(runId), cancellationToken);
        if (result.IsFailure)
        {
            if (result.Error.Code.Contains("NotFound"))
                return NotFound(ApiResponse<GenerateIrppRegularizationsResultDto>.Fail(result.Error.Description));
            return BadRequest(ApiResponse<GenerateIrppRegularizationsResultDto>.Fail(result.Error.Description));
        }

        var dto = result.Value;
        var message = dto.Created + dto.Updated == 0
            ? "Aucune régularisation à générer pour ce cycle."
            : $"{dto.Created} régularisation(s) créée(s), {dto.Updated} mise(s) à jour.";

        return Ok(ApiResponse<GenerateIrppRegularizationsResultDto>.Ok(dto, message));
    }

    /// <summary>Crée ou ajuste manuellement une régularisation.</summary>
    [HttpPost("regularizations")]
    [Authorize(Policy = PermissionPolicies.PayrollRun)]
    [Authorize(Policy = PermissionPolicies.PayrollFirmOperation)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Upsert([FromBody] UpsertIrppRegularizationDto dto, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpsertIrppRegularizationCommand(dto), cancellationToken);
        if (result.IsFailure)
        {
            if (result.Error.Code.Contains("NotFound"))
                return NotFound(ApiResponse<Guid>.Fail(result.Error.Description));
            return BadRequest(ApiResponse<Guid>.Fail(result.Error.Description));
        }
        return Ok(ApiResponse<Guid>.Ok(result.Value, "Régularisation enregistrée."));
    }

    /// <summary>Supprime une régularisation (mois non verrouillé uniquement).</summary>
    [HttpDelete("regularizations/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.PayrollRun)]
    [Authorize(Policy = PermissionPolicies.PayrollFirmOperation)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DeleteIrppRegularizationCommand(id), cancellationToken);
        if (result.IsFailure)
        {
            if (result.Error.Code.Contains("NotFound"))
                return NotFound(ApiResponse<object>.Fail(result.Error.Description));
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        }
        return Ok(ApiResponse<object>.Ok(null!, "Régularisation supprimée."));
    }
}
