using FactuTrust.API.Authorization;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.BankDeposits.Commands;
using FactuTrust.Application.Features.BankDeposits.Queries;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FactuTrust.API.Controllers;

/// <summary>
/// Bank deposits (remises en banque) from cash desk to treasury accounts.
/// </summary>
[ApiController]
[Route("api/bank-deposits")]
[Authorize]
public class BankDepositController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<BankDepositController> _logger;

    public BankDepositController(IMediator mediator, ILogger<BankDepositController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.PaymentsCreate)]
    [ProducesResponseType(typeof(ApiResponse<BankDepositListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateBankDepositRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new CreateBankDepositCommand(request), cancellationToken);
        if (result.IsFailure)
        {
            _logger.LogWarning("Bank deposit creation failed: {Code} - {Description}", result.Error.Code, result.Error.Description);
            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        }

        return Ok(ApiResponse<BankDepositListItemDto>.Ok(result.Value, "Remise en banque enregistrée."));
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.PaymentsRead)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<BankDepositListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int year,
        [FromQuery] int month,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetBankDepositListQuery(year, month, page, pageSize), cancellationToken);
        return Ok(ApiResponse<PagedResult<BankDepositListItemDto>>.Ok(result));
    }

    [HttpGet("preview-number")]
    [Authorize(Policy = PermissionPolicies.PaymentsRead)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PreviewNumber([FromQuery] int year, CancellationToken cancellationToken = default)
    {
        var value = await _mediator.Send(new GetBankDepositPreviewNumberQuery(year), cancellationToken);
        return Ok(ApiResponse<string>.Ok(value));
    }

    /// <summary>
    /// Net balance for the given deposit type up to and including asOfDate (aligned with create validation).
    /// </summary>
    [HttpGet("available-balance")]
    [Authorize(Policy = PermissionPolicies.PaymentsRead)]
    [ProducesResponseType(typeof(ApiResponse<BankDepositAvailableBalanceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAvailableBalance(
        [FromQuery] BankDepositType depositType,
        [FromQuery] DateTime asOfDate,
        CancellationToken cancellationToken = default)
    {
        var dto = await _mediator.Send(
            new GetBankDepositAvailableBalanceQuery(depositType, asOfDate),
            cancellationToken);
        return Ok(ApiResponse<BankDepositAvailableBalanceDto>.Ok(dto));
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = PermissionPolicies.PaymentsUpdate)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(
        [FromRoute] Guid id,
        [FromBody] CancelBankDepositRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new CancelBankDepositCommand(id, request), cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error.Code.EndsWith(".NotFound", StringComparison.Ordinal))
                return NotFound(ApiResponse<object>.Fail(result.Error.Description));

            return BadRequest(ApiResponse<object>.Fail(result.Error.Description));
        }

        _logger.LogInformation("Bank deposit {DepositId} cancelled", id);
        return Ok(ApiResponse<object>.Ok(null!, "Remise en banque annulée."));
    }
}
