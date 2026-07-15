using MediatR;

namespace FactuTrust.Application.Features.Payroll.Leaves;

/// <summary>Calcule le nombre de jours ouvrables (lun–ven) entre deux dates incluses.</summary>
public sealed record ComputeLeaveDaysQuery(DateTime StartDate, DateTime EndDate) : IRequest<int>;

public sealed class ComputeLeaveDaysQueryHandler : IRequestHandler<ComputeLeaveDaysQuery, int>
{
    public Task<int> Handle(ComputeLeaveDaysQuery request, CancellationToken cancellationToken)
    {
        if (request.EndDate < request.StartDate)
            return Task.FromResult(0);

        var days = 0;
        for (var d = request.StartDate.Date; d <= request.EndDate.Date; d = d.AddDays(1))
        {
            if (d.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
                days++;
        }

        return Task.FromResult(days);
    }
}
