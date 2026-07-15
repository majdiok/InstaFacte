using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Enums;
using FactuTrust.Domain.ValueObjects;
using MediatR;

namespace FactuTrust.Application.Features.BankDeposits.Queries;

public sealed record GetBankDepositAvailableBalanceQuery(
    BankDepositType DepositType,
    DateTime AsOfDate) : IRequest<BankDepositAvailableBalanceDto>;

public sealed class GetBankDepositAvailableBalanceQueryHandler
    : IRequestHandler<GetBankDepositAvailableBalanceQuery, BankDepositAvailableBalanceDto>
{
    private readonly ICashOperationRepository _cashOperationRepository;

    public GetBankDepositAvailableBalanceQueryHandler(ICashOperationRepository cashOperationRepository)
    {
        _cashOperationRepository = cashOperationRepository;
    }

    public async Task<BankDepositAvailableBalanceDto> Handle(
        GetBankDepositAvailableBalanceQuery request,
        CancellationToken cancellationToken)
    {
        var method = request.DepositType.ToPaymentMethod();
        var amount = await _cashOperationRepository.GetNetBalanceForMethodUpToDateAsync(
            method,
            request.AsOfDate,
            cancellationToken);

        return new BankDepositAvailableBalanceDto
        {
            Amount = amount,
            Currency = Money.DefaultCurrency
        };
    }
}
