using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using AuditActions = FactuTrust.Domain.Entities.AuditActions;
using MediatR;

namespace FactuTrust.Application.Features.StockVouchers.Commands;

public sealed record ValidateStockVoucherCommand(
    Guid Id,
    IReadOnlyList<StockVoucherLineAllocationsDto>? LineAllocations = null) : IRequest<Result>;

public sealed class ValidateStockVoucherCommandHandler : IRequestHandler<ValidateStockVoucherCommand, Result>
{
    private readonly IStockVoucherMovementService _movementService;
    private readonly IAuditService _auditService;

    public ValidateStockVoucherCommandHandler(
        IStockVoucherMovementService movementService,
        IAuditService auditService)
    {
        _movementService = movementService;
        _auditService = auditService;
    }

    public async Task<Result> Handle(ValidateStockVoucherCommand request, CancellationToken cancellationToken)
    {
        var result = request.LineAllocations is { Count: > 0 }
            ? await _movementService.ValidateAsync(request.Id, request.LineAllocations, cancellationToken)
            : await _movementService.ValidateAsync(request.Id, cancellationToken);
        if (result.IsFailure)
            return result;

        await _auditService.LogAsync(
            AuditActions.StockVoucher.Validated,
            "StockVoucher",
            request.Id,
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}
