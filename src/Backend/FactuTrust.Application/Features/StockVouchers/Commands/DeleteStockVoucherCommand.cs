using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using AuditActions = FactuTrust.Domain.Entities.AuditActions;
using MediatR;

namespace FactuTrust.Application.Features.StockVouchers.Commands;

public sealed record DeleteStockVoucherCommand(Guid Id) : IRequest<Result>;

public sealed class DeleteStockVoucherCommandHandler : IRequestHandler<DeleteStockVoucherCommand, Result>
{
    private readonly IStockVoucherRepository _repository;
    private readonly IAuditService _auditService;

    public DeleteStockVoucherCommandHandler(
        IStockVoucherRepository repository,
        IAuditService auditService)
    {
        _repository = repository;
        _auditService = auditService;
    }

    public async Task<Result> Handle(DeleteStockVoucherCommand request, CancellationToken cancellationToken)
    {
        var voucher = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (voucher is null)
            return Result.Failure(Error.NotFound("StockVoucher", request.Id));

        if (!voucher.Status.CanBeDeleted())
            return Result.Failure(Error.Validation("Status", "Seuls les brouillons peuvent être supprimés."));

        var number = voucher.Number.Value;
        await _repository.DeleteAsync(voucher, cancellationToken);

        await _auditService.LogAsync(
            AuditActions.StockVoucher.Deleted,
            "StockVoucher",
            voucher.Id,
            newValues: new { Number = number },
            cancellationToken: cancellationToken);

        return Result.Success();
    }
}
