using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities;
using FactuTrust.Domain.Enums;
using MediatR;
using AuditActions = FactuTrust.Domain.Entities.AuditActions;

namespace FactuTrust.Application.Features.CashDesk.Commands;

/// <summary>
/// Command to cancel (soft-delete) a cash desk operation.
/// </summary>
public sealed record CancelCashOperationCommand(Guid OperationId, CancelCashOperationRequest Request) : IRequest<Result>;

public sealed class CancelCashOperationCommandHandler : IRequestHandler<CancelCashOperationCommand, Result>
{
    private readonly ICashOperationRepository _cashOperationRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;

    public CancelCashOperationCommandHandler(
        ICashOperationRepository cashOperationRepository,
        ICurrentUser currentUser,
        IAuditService auditService)
    {
        _cashOperationRepository = cashOperationRepository;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<Result> Handle(CancelCashOperationCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.TenantId is null)
            return Result.Failure(Error.Unauthorized("Tenant manquant"));

        var operation = await _cashOperationRepository.GetByIdAsync(request.OperationId, cancellationToken);
        if (operation is null)
            return Result.Failure(Error.NotFound("CashOperation", request.OperationId));

        var previousStatus = operation.Status;
        var previousNumber = operation.Number.Value;

        var cancelResult = operation.Cancel(request.Request.CancellationReason);
        if (cancelResult.IsFailure)
            return cancelResult;

        operation.SetAuditInfo(_currentUser.UserId?.ToString() ?? "system", isUpdate: true);

        await _cashOperationRepository.UpdateAsync(operation, cancellationToken);

        await _auditService.LogAsync(
            AuditActions.CashOperation.Cancelled,
            "CashOperation",
            operation.Id,
            oldValues: new
            {
                status = previousStatus.ToString(),
                number = previousNumber,
                cancelledAt = operation.CancelledAt
            },
            newValues: new
            {
                status = operation.Status.ToString(),
                number = operation.Number.Value,
                cancelledAt = operation.CancelledAt,
                cancellationReason = operation.CancellationReason
            },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}
