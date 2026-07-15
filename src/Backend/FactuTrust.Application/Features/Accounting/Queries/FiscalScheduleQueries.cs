using FactuTrust.Application.Common.Interfaces;
using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Accounting.FiscalSchedule;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.Accounting.Queries;

public sealed record GetFiscalScheduleQuery(FiscalScheduleFiltersDto Filters) : IRequest<Result<FiscalScheduleListDto>>;

public sealed class GetFiscalScheduleQueryHandler : IRequestHandler<GetFiscalScheduleQuery, Result<FiscalScheduleListDto>>
{
    private readonly IFiscalScheduleRepository _repository;
    private readonly TimeProvider _timeProvider;

    public GetFiscalScheduleQueryHandler(IFiscalScheduleRepository repository, TimeProvider timeProvider)
    {
        _repository = repository;
        _timeProvider = timeProvider;
    }

    public async Task<Result<FiscalScheduleListDto>> Handle(GetFiscalScheduleQuery request, CancellationToken cancellationToken)
    {
        var criteria = ToCriteria(request.Filters);
        if (criteria.IsFailure)
            return Result.Failure<FiscalScheduleListDto>(criteria.Error);

        var result = await _repository.ListAsync(criteria.Value, cancellationToken);
        var today = _timeProvider.GetLocalNow().DateTime.Date;
        var items = result.Items.Select(e => FiscalScheduleMappings.ToDto(e, today)).ToList();
        var summaryItems = result.SummaryItems.Select(e => FiscalScheduleMappings.ToDto(e, today)).ToList();

        return Result.Success(new FiscalScheduleListDto
        {
            Items = items,
            Summary = FiscalScheduleMappings.BuildSummary(summaryItems, today),
            Page = Math.Max(criteria.Value.Page, 1),
            PageSize = Math.Clamp(criteria.Value.PageSize, 1, 200),
            TotalCount = result.TotalCount
        });
    }

    private static Result<FiscalScheduleQueryCriteria> ToCriteria(FiscalScheduleFiltersDto filters)
    {
        FiscalObligationType? obligationType = null;
        if (filters.ObligationType.HasValue)
        {
            if (!Enum.IsDefined(typeof(FiscalObligationType), filters.ObligationType.Value))
                return Result.Failure<FiscalScheduleQueryCriteria>(Error.Validation("ObligationType", "Type d'obligation fiscale invalide."));
            obligationType = (FiscalObligationType)filters.ObligationType.Value;
        }

        FiscalScheduleStatus? status = null;
        if (filters.Status.HasValue)
        {
            if (!Enum.IsDefined(typeof(FiscalScheduleStatus), filters.Status.Value))
                return Result.Failure<FiscalScheduleQueryCriteria>(Error.Validation("Status", "Statut d'echeance fiscale invalide."));
            status = (FiscalScheduleStatus)filters.Status.Value;
        }

        return Result.Success(new FiscalScheduleQueryCriteria
        {
            FiscalYear = filters.FiscalYear,
            PeriodMonth = filters.PeriodMonth,
            PeriodQuarter = filters.PeriodQuarter,
            ObligationType = obligationType,
            Status = status,
            ResponsibleUserId = filters.ResponsibleUserId,
            DueFrom = filters.DueFrom,
            DueTo = filters.DueTo,
            Search = filters.Search,
            IncludeCancelled = filters.IncludeCancelled,
            Page = filters.Page,
            PageSize = filters.PageSize
        });
    }
}

public sealed record GetFiscalScheduleHistoryQuery(Guid Id) : IRequest<Result<IReadOnlyList<FiscalScheduleHistoryDto>>>;

public sealed class GetFiscalScheduleHistoryQueryHandler : IRequestHandler<GetFiscalScheduleHistoryQuery, Result<IReadOnlyList<FiscalScheduleHistoryDto>>>
{
    private readonly IFiscalScheduleRepository _repository;

    public GetFiscalScheduleHistoryQueryHandler(IFiscalScheduleRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IReadOnlyList<FiscalScheduleHistoryDto>>> Handle(GetFiscalScheduleHistoryQuery request, CancellationToken cancellationToken)
    {
        var exists = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (exists is null)
            return Result.Failure<IReadOnlyList<FiscalScheduleHistoryDto>>(Error.NotFound("FiscalScheduleEntry", request.Id));

        var rows = await _repository.GetHistoryAsync(request.Id, cancellationToken);
        return Result.Success<IReadOnlyList<FiscalScheduleHistoryDto>>(rows.Select(FiscalScheduleMappings.ToDto).ToList());
    }
}

/// <summary>
/// Responsables proposés pour une échéance fiscale : utilisateurs actifs du tenant
/// d'APPARTENANCE de l'appelant (claim tenant_id) — en mode délégué, ce sont donc les
/// collaborateurs du cabinet, pas les utilisateurs du dossier client courant.
/// </summary>
public sealed record GetFiscalAssignableUsersQuery(Guid TargetTenantId) : IRequest<Result<IReadOnlyList<CrmAssignableUserDto>>>;

public sealed class GetFiscalAssignableUsersQueryHandler
    : IRequestHandler<GetFiscalAssignableUsersQuery, Result<IReadOnlyList<CrmAssignableUserDto>>>
{
    private readonly IAssignableTenantUsersSource _users;

    public GetFiscalAssignableUsersQueryHandler(IAssignableTenantUsersSource users)
    {
        _users = users;
    }

    public async Task<Result<IReadOnlyList<CrmAssignableUserDto>>> Handle(
        GetFiscalAssignableUsersQuery request, CancellationToken cancellationToken)
    {
        if (request.TargetTenantId == Guid.Empty)
            return Result.Failure<IReadOnlyList<CrmAssignableUserDto>>(
                Error.Validation("TenantId", "Tenant d'appartenance introuvable."));

        var users = await _users.ListActiveForTenantAsync(request.TargetTenantId, cancellationToken);
        return Result.Success(users);
    }
}
