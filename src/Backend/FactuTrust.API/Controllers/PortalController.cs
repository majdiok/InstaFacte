using FactuTrust.API.Authorization;
using FactuTrust.API.Http;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

[ApiController]
[Route("api/portal")]
[Authorize(Policy = PermissionPolicies.PortalAccess)]
public sealed class PortalController : ControllerBase
{
    private readonly IClientPortalService _portal;

    public PortalController(IClientPortalService portal)
    {
        _portal = portal;
    }

    [HttpGet("me")]
    [Authorize(Policy = PermissionPolicies.PortalProfileRead)]
    [ProducesResponseType(typeof(ApiResponse<PortalMeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMe(CancellationToken cancellationToken)
        => this.ToActionResult(await _portal.GetMeAsync(cancellationToken));

    [HttpGet("summary")]
    [ProducesResponseType(typeof(ApiResponse<PortalSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
        => this.ToActionResult(await _portal.GetSummaryAsync(cancellationToken));

    [HttpGet("invoices")]
    [Authorize(Policy = PermissionPolicies.PortalInvoicesRead)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<PortalInvoiceListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInvoices(
        [FromQuery] InvoiceStatus? status,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] bool unpaidOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        return this.ToActionResult(await _portal.GetInvoicesAsync(
            status, fromDate, toDate, unpaidOnly, page, pageSize, cancellationToken));
    }

    [HttpGet("invoices/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.PortalInvoicesRead)]
    [ProducesResponseType(typeof(ApiResponse<PortalInvoiceDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInvoice(Guid id, CancellationToken cancellationToken)
        => this.ToActionResult(await _portal.GetInvoiceAsync(id, cancellationToken));

    [HttpGet("invoices/{id:guid}/pdf")]
    [Authorize(Policy = PermissionPolicies.PortalInvoicesRead)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInvoicePdf(Guid id, CancellationToken cancellationToken)
    {
        var result = await _portal.GetInvoicePdfAsync(id, cancellationToken);
        if (result.IsFailure)
            return ResultHttp.ToError(this, result.Error);

        return File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
    }

    [HttpGet("payments")]
    [Authorize(Policy = PermissionPolicies.PortalPaymentsRead)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PortalPaymentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPayments(
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken cancellationToken = default)
        => this.ToActionResult(await _portal.GetPaymentsAsync(fromDate, toDate, cancellationToken));

    [HttpGet("statement")]
    [Authorize(Policy = PermissionPolicies.PortalPaymentsRead)]
    [ProducesResponseType(typeof(ApiResponse<PortalStatementDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatement(
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken cancellationToken = default)
        => this.ToActionResult(await _portal.GetStatementAsync(fromDate, toDate, cancellationToken));
}
