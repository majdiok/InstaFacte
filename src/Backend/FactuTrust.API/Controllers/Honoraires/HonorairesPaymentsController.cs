using FactuTrust.API.Authorization;
using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers.Honoraires;

[ApiController]
[Route("api/honoraires/payments")]
[Authorize]
public sealed class HonorairesPaymentsController : ControllerBase
{
    private readonly IHonorairesBillingService _service;

    public HonorairesPaymentsController(IHonorairesBillingService service) => _service = service;

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.HonorairesPaymentsRead)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<HonorairesPaymentDto>>>> List(
        [FromQuery] Guid? invoiceId,
        CancellationToken cancellationToken = default)
    {
        var items = await _service.ListPaymentsAsync(invoiceId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<HonorairesPaymentDto>>.Ok(items));
    }
}
