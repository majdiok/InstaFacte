using FactuTrust.Domain.Services.Payroll;
using MediatR;

namespace FactuTrust.Application.Features.Payroll.Leaves;

/// <summary>Calcule le nombre de jours ouvrables (lun–ven) entre deux dates incluses.</summary>
public sealed record ComputeLeaveDaysQuery(DateTime StartDate, DateTime EndDate) : IRequest<int>;

public sealed class ComputeLeaveDaysQueryHandler : IRequestHandler<ComputeLeaveDaysQuery, int>
{
    public Task<int> Handle(ComputeLeaveDaysQuery request, CancellationToken cancellationToken) =>
        Task.FromResult(PayrollWorkingDaysCounter.CountWeekdays(request.StartDate, request.EndDate));
}
