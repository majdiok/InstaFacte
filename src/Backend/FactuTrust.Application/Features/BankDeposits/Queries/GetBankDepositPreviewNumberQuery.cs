using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Domain.Services;
using MediatR;

namespace FactuTrust.Application.Features.BankDeposits.Queries;

public sealed record GetBankDepositPreviewNumberQuery(int Year) : IRequest<string>;

public sealed class GetBankDepositPreviewNumberQueryHandler
    : IRequestHandler<GetBankDepositPreviewNumberQuery, string>
{
    private readonly IBankDepositNumberGenerator _generator;
    private readonly ICurrentUser _currentUser;

    public GetBankDepositPreviewNumberQueryHandler(
        IBankDepositNumberGenerator generator,
        ICurrentUser currentUser)
    {
        _generator = generator;
        _currentUser = currentUser;
    }

    public async Task<string> Handle(GetBankDepositPreviewNumberQuery request, CancellationToken cancellationToken)
    {
        if (_currentUser.TenantId is null)
            return string.Empty;

        return await _generator.PreviewNextNumberAsync(
            _currentUser.TenantId.Value,
            request.Year,
            cancellationToken);
    }
}
