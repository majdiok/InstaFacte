using FactuTrust.API.Authorization;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Payroll.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>
/// Vue d'ensemble de la paie d'un mois : indicateurs, répartitions, série de l'exercice,
/// salariés du cycle et échéances sociales.
/// </summary>
/// <remarks>
/// Volontairement sous <c>api/payroll</c> et non <c>api/firm/payroll</c> : la paie interne du
/// cabinet et celle d'un dossier client lisent les mêmes tables du tenant courant. Le même
/// endpoint sert donc les deux écrans, sans duplication d'agrégation.
/// </remarks>
[ApiController]
[Route("api/payroll/dashboard")]
[Authorize]
public class PayrollDashboardController : ControllerBase
{
    private readonly IMediator _mediator;

    public PayrollDashboardController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Tableau de bord du mois demandé, ou du mois courant par défaut.</summary>
    [HttpGet]
    [Authorize(Policy = PermissionPolicies.PayrollRead)]
    [ProducesResponseType(typeof(ApiResponse<PayrollDashboardDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] int? year,
        [FromQuery] int? month,
        CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow;
        var resolvedYear = year ?? today.Year;
        var resolvedMonth = month ?? today.Month;

        if (resolvedYear is < 2000 or > 2100)
            return BadRequest(ApiResponse<PayrollDashboardDto>.Fail("L'année doit être comprise entre 2000 et 2100."));
        if (resolvedMonth is < 1 or > 12)
            return BadRequest(ApiResponse<PayrollDashboardDto>.Fail("Le mois doit être compris entre 1 et 12."));

        var data = await _mediator.Send(
            new GetPayrollDashboardQuery(resolvedYear, resolvedMonth), cancellationToken);

        return Ok(ApiResponse<PayrollDashboardDto>.Ok(data));
    }
}
