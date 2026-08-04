using FactuTrust.API.Authorization;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers.Honoraires;

[ApiController]
[Route("api/honoraires/dossiers")]
[Authorize]
public sealed class HonorairesDossiersController : ControllerBase
{
    private readonly IHonorairesBillingService _service;

    public HonorairesDossiersController(IHonorairesBillingService service) => _service = service;

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.HonorairesInvoicesRead)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<BillableDossierDto>>>> List(CancellationToken cancellationToken)
    {
        var items = await _service.ListBillableDossiersAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<BillableDossierDto>>.Ok(items));
    }
}
