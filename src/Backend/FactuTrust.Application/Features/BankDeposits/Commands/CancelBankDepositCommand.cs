using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using MediatR;
using AuditActions = FactuTrust.Domain.Entities.AuditActions;

namespace FactuTrust.Application.Features.BankDeposits.Commands;

public sealed record CancelBankDepositCommand(Guid BankDepositId, CancelBankDepositRequest Request) : IRequest<Result>;

public sealed class CancelBankDepositCommandHandler : IRequestHandler<CancelBankDepositCommand, Result>
{
    private readonly IBankDepositRepository _bankDepositRepository;
    private readonly ICashOperationRepository _cashOperationRepository;
    private readonly ICurrentUser _currentUser;
    private readonly IAuditService _auditService;

    public CancelBankDepositCommandHandler(
        IBankDepositRepository bankDepositRepository,
        ICashOperationRepository cashOperationRepository,
        ICurrentUser currentUser,
        IAuditService auditService)
    {
        _bankDepositRepository = bankDepositRepository;
        _cashOperationRepository = cashOperationRepository;
        _currentUser = currentUser;
        _auditService = auditService;
    }

    public async Task<Result> Handle(CancelBankDepositCommand request, CancellationToken cancellationToken)
    {
        if (_currentUser.TenantId is null)
            return Result.Failure(Error.Unauthorized("Tenant manquant"));

        var deposit = await _bankDepositRepository.GetByIdAsync(request.BankDepositId, cancellationToken);
        if (deposit is null)
            return Result.Failure(Error.NotFound("BankDeposit", request.BankDepositId));

        var operation = await _cashOperationRepository.GetByIdAsync(deposit.CashOperationId, cancellationToken);
        if (operation is null)
            return Result.Failure(Error.NotFound("CashOperation", deposit.CashOperationId));

        var previousDepositStatus = deposit.Status;
        var previousOpStatus = operation.Status;
        var previousOpNumber = operation.Number.Value;

        var cancelDeposit = deposit.Cancel(request.Request.CancellationReason);
        if (cancelDeposit.IsFailure)
            return cancelDeposit;

        var cancelOp = operation.Cancel(request.Request.CancellationReason);
        if (cancelOp.IsFailure)
            return cancelOp;

        var userId = _currentUser.UserId?.ToString() ?? "system";
        deposit.SetAuditInfo(userId, isUpdate: true);
        operation.SetAuditInfo(userId, isUpdate: true);

        await _bankDepositRepository.UpdateBankDepositAndCashOperationAsync(deposit, operation, cancellationToken);

        await _auditService.LogAsync(
            AuditActions.BankDeposit.Cancelled,
            "BankDeposit",
            deposit.Id,
            oldValues: new { status = previousDepositStatus.ToString() },
            newValues: new
            {
                status = deposit.Status.ToString(),
                cancelledAt = deposit.CancelledAt,
                cancellationReason = deposit.CancellationReason
            },
            cancellationToken: cancellationToken);

        await _auditService.LogAsync(
            AuditActions.CashOperation.Cancelled,
            "CashOperation",
            operation.Id,
            oldValues: new
            {
                status = previousOpStatus.ToString(),
                number = previousOpNumber
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
