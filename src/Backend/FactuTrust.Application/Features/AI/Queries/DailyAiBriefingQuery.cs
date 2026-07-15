using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.Queries;
using FactuTrust.Application.Features.CRM;
using FactuTrust.Application.Features.Reports.Queries;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.AI.Queries;

public sealed record DailyAiBriefingQuery(Guid UserId) : IRequest<Result<DailyAiBriefingDto>>;

public sealed class DailyAiBriefingDto
{
    public int UpcomingRemindersCount { get; init; }
    public IReadOnlyList<DailyBriefingReminderItemDto> NextReminders { get; init; } = Array.Empty<DailyBriefingReminderItemDto>();
    public decimal TotalClientBalances { get; init; }
    public int ClientsWithBalanceCount { get; init; }
    public decimal AgingOver90 { get; init; }
    public string Currency { get; init; } = "TND";
    public DateTime GeneratedAtUtc { get; init; }
}

public sealed record DailyBriefingReminderItemDto(string Subject, DateTime? DueDate, string? ClientName);

public sealed class DailyAiBriefingQueryHandler : IRequestHandler<DailyAiBriefingQuery, Result<DailyAiBriefingDto>>
{
    private readonly IMediator _mediator;

    public DailyAiBriefingQueryHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<Result<DailyAiBriefingDto>> Handle(DailyAiBriefingQuery request, CancellationToken cancellationToken)
    {
        var remindersResult = await _mediator.Send(new GetMyRemindersQuery(request.UserId), cancellationToken);
        var agingResult = await _mediator.Send(new GetClientAgingReportQuery(), cancellationToken);
        var balancesResult = await _mediator.Send(new GetClientBalancesReportQuery(), cancellationToken);

        IReadOnlyList<SalesActivityDto> reminders = remindersResult.IsSuccess
            ? remindersResult.Value
            : Array.Empty<SalesActivityDto>();
        var upcoming = reminders
            .Where(r => r.DueDate == null || r.DueDate.Value.Date <= DateTime.UtcNow.Date.AddDays(7))
            .OrderBy(r => r.DueDate ?? DateTime.MaxValue)
            .Take(5)
            .Select(r => new DailyBriefingReminderItemDto(r.Subject, r.DueDate, r.ClientName))
            .ToList();

        decimal totalBalances = 0;
        var balCount = 0;
        if (balancesResult.IsSuccess)
        {
            var rows = balancesResult.Value;
            balCount = rows.Count(r => r.Balance > 0);
            totalBalances = rows.Sum(r => r.Balance);
        }

        decimal over90 = 0;
        if (agingResult.IsSuccess)
        {
            foreach (var row in agingResult.Value)
                over90 += row.DaysOver90;
        }

        var dto = new DailyAiBriefingDto
        {
            UpcomingRemindersCount = reminders.Count(r => r.DueDate == null || r.DueDate.Value.Date <= DateTime.UtcNow.Date.AddDays(7)),
            NextReminders = upcoming,
            TotalClientBalances = totalBalances,
            ClientsWithBalanceCount = balCount,
            AgingOver90 = over90,
            GeneratedAtUtc = DateTime.UtcNow
        };

        return Result.Success(dto);
    }
}
