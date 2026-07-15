using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.BankAccounts;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.BankAccounts.Queries;

public sealed record GetBankAccountsQuery : IRequest<Result<IReadOnlyList<BankAccountDto>>>;

public sealed class GetBankAccountsQueryHandler : IRequestHandler<GetBankAccountsQuery, Result<IReadOnlyList<BankAccountDto>>>
{
    private readonly IBankAccountRepository _bankAccountRepository;
    private readonly IEnsureDefaultCompanyService _ensureDefaultCompanyService;
    private readonly ICurrentUser _currentUser;

    public GetBankAccountsQueryHandler(
        IBankAccountRepository bankAccountRepository,
        IEnsureDefaultCompanyService ensureDefaultCompanyService,
        ICurrentUser currentUser)
    {
        _bankAccountRepository = bankAccountRepository;
        _ensureDefaultCompanyService = ensureDefaultCompanyService;
        _currentUser = currentUser;
    }

    public async Task<Result<IReadOnlyList<BankAccountDto>>> Handle(GetBankAccountsQuery request, CancellationToken cancellationToken)
    {
        if (_currentUser.TenantId is null)
            return Result.Failure<IReadOnlyList<BankAccountDto>>(Error.Unauthorized("Tenant manquant"));

        var companyIdResult = await _ensureDefaultCompanyService.GetOrCreateDefaultCompanyIdAsync(cancellationToken);
        if (companyIdResult.IsFailure)
            return Result.Failure<IReadOnlyList<BankAccountDto>>(companyIdResult.Error);

        var list = await _bankAccountRepository.GetActiveByCompanyAsync(companyIdResult.Value, cancellationToken);
        return Result.Success<IReadOnlyList<BankAccountDto>>(list.Select(b => b.ToDto()).ToList());
    }
}
