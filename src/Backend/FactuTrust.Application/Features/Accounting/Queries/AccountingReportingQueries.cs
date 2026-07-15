using FactuTrust.Application.Common.Interfaces.Services;
using FactuTrust.Application.DTOs;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Accounting.Queries;

public sealed record GetChartOfAccountsQuery : IRequest<Result<IReadOnlyList<ChartOfAccountDto>>>;

public sealed class GetChartOfAccountsQueryHandler
    : IRequestHandler<GetChartOfAccountsQuery, Result<IReadOnlyList<ChartOfAccountDto>>>
{
    private readonly IAccountingReportingService _reporting;

    public GetChartOfAccountsQueryHandler(IAccountingReportingService reporting)
    {
        _reporting = reporting;
    }

    public Task<Result<IReadOnlyList<ChartOfAccountDto>>> Handle(GetChartOfAccountsQuery request, CancellationToken cancellationToken)
        => _reporting.GetChartOfAccountsAsync(cancellationToken);
}

public sealed record GetJournalEntriesQuery(string? JournalCode, DateTime From, DateTime To)
    : IRequest<Result<IReadOnlyList<JournalEntryDto>>>;

public sealed class GetJournalEntriesQueryHandler
    : IRequestHandler<GetJournalEntriesQuery, Result<IReadOnlyList<JournalEntryDto>>>
{
    private readonly IAccountingReportingService _reporting;

    public GetJournalEntriesQueryHandler(IAccountingReportingService reporting)
    {
        _reporting = reporting;
    }

    public Task<Result<IReadOnlyList<JournalEntryDto>>> Handle(GetJournalEntriesQuery request, CancellationToken cancellationToken)
        => _reporting.GetJournalEntriesAsync(request.JournalCode, request.From, request.To, cancellationToken);
}

public sealed record GetLedgerQuery(string AccountNumber, DateTime From, DateTime To)
    : IRequest<Result<IReadOnlyList<LedgerRowDto>>>;

public sealed class GetLedgerQueryHandler : IRequestHandler<GetLedgerQuery, Result<IReadOnlyList<LedgerRowDto>>>
{
    private readonly IAccountingReportingService _reporting;

    public GetLedgerQueryHandler(IAccountingReportingService reporting)
    {
        _reporting = reporting;
    }

    public Task<Result<IReadOnlyList<LedgerRowDto>>> Handle(GetLedgerQuery request, CancellationToken cancellationToken)
        => _reporting.GetLedgerAsync(request.AccountNumber, request.From, request.To, cancellationToken);
}

public sealed record GetBalanceQuery(DateTime From, DateTime To)
    : IRequest<Result<IReadOnlyList<BalanceRowDto>>>;

public sealed class GetBalanceQueryHandler : IRequestHandler<GetBalanceQuery, Result<IReadOnlyList<BalanceRowDto>>>
{
    private readonly IAccountingReportingService _reporting;

    public GetBalanceQueryHandler(IAccountingReportingService reporting)
    {
        _reporting = reporting;
    }

    public Task<Result<IReadOnlyList<BalanceRowDto>>> Handle(GetBalanceQuery request, CancellationToken cancellationToken)
        => _reporting.GetBalanceAsync(request.From, request.To, cancellationToken);
}

public sealed record GetAccountingDashboardQuery : IRequest<Result<AccountingDashboardDto>>;

public sealed class GetAccountingDashboardQueryHandler
    : IRequestHandler<GetAccountingDashboardQuery, Result<AccountingDashboardDto>>
{
    private readonly IAccountingReportingService _reporting;
    private readonly IMediator _mediator;

    public GetAccountingDashboardQueryHandler(IAccountingReportingService reporting, IMediator mediator)
    {
        _reporting = reporting;
        _mediator = mediator;
    }

    public async Task<Result<AccountingDashboardDto>> Handle(GetAccountingDashboardQuery request, CancellationToken cancellationToken)
    {
        var dashboard = await _reporting.GetDashboardAsync(cancellationToken);
        if (dashboard.IsFailure)
            return dashboard;

        // Single source of truth: the dashboard "TVA due" estimate must match the dedicated
        // VAT declaration page exactly (same invoice/purchase source + carried-over credit) for
        // the current month — instead of the parallel journal-based approximation.
        var now = DateTime.UtcNow;
        var vat = await _mediator.Send(new GetVatDeclarationQuery(now.Year, now.Month), cancellationToken);
        if (vat.IsSuccess)
            return Result.Success(dashboard.Value with { VatDueEstimate = vat.Value.VatDue });

        // Fallback: keep the service's journal-based estimate if the declaration cannot be computed.
        return dashboard;
    }
}

public sealed record GetAccountingPeriodsQuery : IRequest<Result<IReadOnlyList<AccountingPeriodDto>>>;

public sealed class GetAccountingPeriodsQueryHandler
    : IRequestHandler<GetAccountingPeriodsQuery, Result<IReadOnlyList<AccountingPeriodDto>>>
{
    private readonly IAccountingReportingService _reporting;

    public GetAccountingPeriodsQueryHandler(IAccountingReportingService reporting)
    {
        _reporting = reporting;
    }

    public Task<Result<IReadOnlyList<AccountingPeriodDto>>> Handle(GetAccountingPeriodsQuery request, CancellationToken cancellationToken)
        => _reporting.GetPeriodsAsync(cancellationToken);
}
