using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Reports.Queries;

public sealed record GetClientWithholdingsReportQuery(DateTime FromDate, DateTime ToDate)
    : IRequest<Result<IReadOnlyList<ClientWithholdingReportRowDto>>>;

public sealed class GetClientWithholdingsReportQueryHandler
    : IRequestHandler<GetClientWithholdingsReportQuery, Result<IReadOnlyList<ClientWithholdingReportRowDto>>>
{
    private readonly IPaymentRepository _paymentRepository;

    public GetClientWithholdingsReportQueryHandler(IPaymentRepository paymentRepository)
    {
        _paymentRepository = paymentRepository;
    }

    public async Task<Result<IReadOnlyList<ClientWithholdingReportRowDto>>> Handle(
        GetClientWithholdingsReportQuery request,
        CancellationToken cancellationToken)
    {
        var rows = await _paymentRepository.GetClientWithholdingReportRowsAsync(
            request.FromDate,
            request.ToDate,
            cancellationToken);

        return Result.Success<IReadOnlyList<ClientWithholdingReportRowDto>>(rows);
    }
}