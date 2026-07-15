using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Accounting.Queries;

public sealed record GetPreClosingChecklistQuery(int FiscalYear) : IRequest<Result<PreClosingChecklistDto>>;

public sealed class GetPreClosingChecklistQueryHandler
    : IRequestHandler<GetPreClosingChecklistQuery, Result<PreClosingChecklistDto>>
{
    private readonly IPreClosingControlService _service;

    public GetPreClosingChecklistQueryHandler(IPreClosingControlService service)
    {
        _service = service;
    }

    public Task<Result<PreClosingChecklistDto>> Handle(GetPreClosingChecklistQuery request, CancellationToken cancellationToken)
        => _service.RunAsync(request.FiscalYear, cancellationToken);
}
