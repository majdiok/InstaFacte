using FactuTrust.API.Authorization;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.CashRegister;
using FactuTrust.Domain.Common;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>
/// POS cart, held tickets, cash-register session (vacation), X-report and Z-close.
/// Cart persistence is tenant SQL (<see cref="SavePosCartDraftCommand"/>), not in-memory.
/// </summary>
[ApiController]
[Route("api/pos")]
[Authorize]
public sealed class PosSessionController : ControllerBase
{
    private readonly IMediator _mediator;

    public PosSessionController(IMediator mediator)
    {
        _mediator = mediator;
    }

    public sealed class PosSessionStateDto
    {
        public object? State { get; set; }
    }

    [HttpGet("register")]
    [Authorize(Policy = PermissionPolicies.InvoicesCreate)]
    [ProducesResponseType(typeof(ApiResponse<CashRegisterDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRegister(
        [FromQuery] Guid warehouseId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetCashRegisterQuery(warehouseId), cancellationToken);
        return ToPosResult(result);
    }

    [HttpGet("registers")]
    [Authorize(Policy = PermissionPolicies.InvoicesCreate)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<CashRegisterDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListRegisters(
        [FromQuery] Guid warehouseId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ListCashRegistersQuery(warehouseId), cancellationToken);
        return ToPosResult(result);
    }

    [HttpPost("registers")]
    [Authorize(Policy = PermissionPolicies.PaymentsCreate)]
    [ProducesResponseType(typeof(ApiResponse<CashRegisterDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateRegister(
        [FromBody] CreateCashRegisterRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new CreateCashRegisterCommand(request), cancellationToken);
        return ToPosResult(result);
    }

    [HttpGet("open-session")]
    [Authorize(Policy = PermissionPolicies.InvoicesCreate)]
    [ProducesResponseType(typeof(ApiResponse<CashRegisterSessionDto?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOpenSession(
        [FromQuery] Guid warehouseId,
        [FromQuery] Guid? cashRegisterId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new GetOpenCashRegisterSessionQuery(warehouseId, cashRegisterId), cancellationToken);
        return ToPosResult(result);
    }

    [HttpPost("session/open")]
    [Authorize(Policy = PermissionPolicies.PaymentsCreate)]
    [ProducesResponseType(typeof(ApiResponse<CashRegisterSessionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> OpenSession(
        [FromBody] OpenCashRegisterSessionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new OpenCashRegisterSessionCommand(request), cancellationToken);
        return ToPosResult(result);
    }

    [HttpGet("session/x-report")]
    [Authorize(Policy = PermissionPolicies.InvoicesCreate)]
    [ProducesResponseType(typeof(ApiResponse<PosSessionReportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetXReport(
        [FromQuery] Guid warehouseId,
        [FromQuery] Guid? cashRegisterId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new GetPosSessionXReportQuery(warehouseId, cashRegisterId), cancellationToken);
        return ToPosResult(result);
    }

    [HttpPost("session/close")]
    [Authorize(Policy = PermissionPolicies.PaymentsCreate)]
    [ProducesResponseType(typeof(ApiResponse<PosSessionReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CloseSession(
        [FromQuery] Guid warehouseId,
        [FromQuery] Guid? cashRegisterId,
        [FromBody] CloseCashRegisterSessionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new CloseCashRegisterSessionCommand(warehouseId, request, cashRegisterId), cancellationToken);
        return ToPosResult(result);
    }

    [HttpGet("z-reports")]
    [Authorize(Policy = PermissionPolicies.InvoicesCreate)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<ZReportListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListZReports(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] Guid? registerId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ListZReportsQuery(from, to, registerId), cancellationToken);
        return ToPosResult(result);
    }

    [HttpGet("z-reports/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.InvoicesCreate)]
    [ProducesResponseType(typeof(ApiResponse<PosSessionReportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetZReport(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetZReportQuery(id), cancellationToken);
        return ToPosResult(result);
    }

    [HttpGet("cart")]
    [Authorize(Policy = PermissionPolicies.InvoicesCreate)]
    [ProducesResponseType(typeof(ApiResponse<PosCartStateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCart(
        [FromQuery] Guid? warehouseId,
        [FromQuery] Guid? cashRegisterId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetPosCartDraftQuery(warehouseId, cashRegisterId), cancellationToken);
        return ToPosResult(result);
    }

    [HttpPut("cart")]
    [Authorize(Policy = PermissionPolicies.InvoicesCreate)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SaveCart(
        [FromBody] SavePosCartRequest body,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new SavePosCartDraftCommand(body.WarehouseId, body.State, body.CashRegisterId), cancellationToken);
        return ToPosResult(result);
    }

    [HttpDelete("cart")]
    [Authorize(Policy = PermissionPolicies.InvoicesCreate)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ClearCart(
        [FromQuery] Guid? warehouseId,
        [FromQuery] Guid? cashRegisterId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new ClearPosCartDraftCommand(warehouseId, cashRegisterId), cancellationToken);
        return ToPosResult(result);
    }

    /// <summary>Legacy cart GET — same payload as <see cref="GetCart"/>.</summary>
    [HttpGet("session")]
    [Authorize(Policy = PermissionPolicies.InvoicesCreate)]
    [ProducesResponseType(typeof(ApiResponse<PosSessionStateDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSession(
        [FromQuery] Guid? warehouseId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetPosCartDraftQuery(warehouseId), cancellationToken);
        if (result.IsFailure)
            return ToPosError(result.Error);

        return Ok(ApiResponse<PosSessionStateDto>.Ok(new PosSessionStateDto { State = result.Value.State }));
    }

    /// <summary>Legacy cart POST — same as <see cref="SaveCart"/>.</summary>
    [HttpPost("session")]
    [Authorize(Policy = PermissionPolicies.InvoicesCreate)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SaveSession(
        [FromBody] SavePosCartRequest? body,
        [FromQuery] Guid? warehouseId,
        CancellationToken cancellationToken)
    {
        if (body is null)
            return BadRequest(ApiResponse<object?>.Fail("État POS manquant"));

        var resolvedWarehouse = body.WarehouseId ?? warehouseId;
        var result = await _mediator.Send(
            new SavePosCartDraftCommand(resolvedWarehouse, body.State, body.CashRegisterId), cancellationToken);
        return ToPosResult(result);
    }

    [HttpPost("session/clear")]
    [Authorize(Policy = PermissionPolicies.InvoicesCreate)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ClearSession(
        [FromQuery] Guid? warehouseId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ClearPosCartDraftCommand(warehouseId), cancellationToken);
        return ToPosResult(result);
    }

    [HttpGet("held-tickets")]
    [Authorize(Policy = PermissionPolicies.InvoicesCreate)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<PosHeldTicketDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListHeldTickets(
        [FromQuery] Guid warehouseId,
        [FromQuery] Guid? cashRegisterId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new ListPosHeldTicketsQuery(warehouseId, cashRegisterId), cancellationToken);
        return ToPosResult(result);
    }

    [HttpPost("held-tickets")]
    [Authorize(Policy = PermissionPolicies.InvoicesCreate)]
    [ProducesResponseType(typeof(ApiResponse<PosHeldTicketDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SaveHeldTicket(
        [FromBody] SavePosHeldTicketRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new SavePosHeldTicketCommand(request), cancellationToken);
        return ToPosResult(result);
    }

    [HttpPost("held-tickets/import")]
    [Authorize(Policy = PermissionPolicies.InvoicesCreate)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ImportHeldTickets(
        [FromBody] ImportPosHeldTicketsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ImportPosHeldTicketsCommand(request), cancellationToken);
        return ToPosResult(result);
    }

    [HttpPost("held-tickets/{id:guid}/recall")]
    [Authorize(Policy = PermissionPolicies.InvoicesCreate)]
    [ProducesResponseType(typeof(ApiResponse<PosHeldTicketDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RecallHeldTicket(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new RecallPosHeldTicketCommand(id), cancellationToken);
        return ToPosResult(result);
    }

    [HttpDelete("held-tickets/{id:guid}")]
    [Authorize(Policy = PermissionPolicies.InvoicesCreate)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteHeldTicket(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DeletePosHeldTicketCommand(id), cancellationToken);
        return ToPosResult(result);
    }

    private IActionResult ToPosResult(Result result)
    {
        if (result.IsSuccess)
            return Ok(ApiResponse<object?>.Ok(null));
        return ToPosError(result.Error);
    }

    private IActionResult ToPosResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
            return Ok(ApiResponse<T>.Ok(result.Value));
        return ToPosError(result.Error);
    }

    private IActionResult ToPosError(Error error)
    {
        var code = error.Code;
        if (code == "POS_SESSION_CLOSED" || code == "Conflict")
            return Conflict(ApiResponse<object>.Fail(error.Description, "POS_SESSION_CLOSED"));
        if (code == "Unauthorized")
            return Unauthorized(ApiResponse<object>.Fail(error.Description, code));
        if (code.Contains("NotFound", StringComparison.Ordinal))
            return NotFound(ApiResponse<object>.Fail(error.Description, code));
        return BadRequest(ApiResponse<object>.Fail(error.Description, code));
    }
}
