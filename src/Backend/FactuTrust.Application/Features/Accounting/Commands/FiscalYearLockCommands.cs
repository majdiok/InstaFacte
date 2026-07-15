using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Accounting.Commands;

public sealed record LockFiscalYearCommand(int FiscalYear) : IRequest<Result>;

public sealed class LockFiscalYearCommandHandler : IRequestHandler<LockFiscalYearCommand, Result>
{
    private readonly IFiscalYearLockService _service;

    public LockFiscalYearCommandHandler(IFiscalYearLockService service)
    {
        _service = service;
    }

    public Task<Result> Handle(LockFiscalYearCommand request, CancellationToken cancellationToken)
        => _service.LockYearAsync(request.FiscalYear, cancellationToken);
}

public sealed record GetFiscalYearLocksQuery : IRequest<Result<IReadOnlyList<FiscalYearLockDto>>>;

public sealed class GetFiscalYearLocksQueryHandler
    : IRequestHandler<GetFiscalYearLocksQuery, Result<IReadOnlyList<FiscalYearLockDto>>>
{
    private readonly IFiscalYearLockService _service;

    public GetFiscalYearLocksQueryHandler(IFiscalYearLockService service)
    {
        _service = service;
    }

    public Task<Result<IReadOnlyList<FiscalYearLockDto>>> Handle(GetFiscalYearLocksQuery request, CancellationToken cancellationToken)
        => _service.GetLocksAsync(cancellationToken);
}
