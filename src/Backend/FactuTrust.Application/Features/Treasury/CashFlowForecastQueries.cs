using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Treasury;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.Treasury.Dtos;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Enums;
using MediatR;

namespace FactuTrust.Application.Features.Treasury;

/// <summary>
/// Projection complète pour l'écran, recalculée si le dernier run a dépassé sa fraîcheur.
/// </summary>
public sealed record GetCashFlowForecastQuery(int HorizonMonths) : IRequest<Result<CashFlowForecastDto>>;

public sealed class GetCashFlowForecastQueryHandler
    : IRequestHandler<GetCashFlowForecastQuery, Result<CashFlowForecastDto>>
{
    private readonly ICashFlowForecastService _forecastService;
    private readonly ICashFlowForecastRepository _repository;
    private readonly ICashFlowForecastSettingsRepository _settingsRepository;

    public GetCashFlowForecastQueryHandler(
        ICashFlowForecastService forecastService,
        ICashFlowForecastRepository repository,
        ICashFlowForecastSettingsRepository settingsRepository)
    {
        _forecastService = forecastService;
        _repository = repository;
        _settingsRepository = settingsRepository;
    }

    public async Task<Result<CashFlowForecastDto>> Handle(
        GetCashFlowForecastQuery request,
        CancellationToken cancellationToken)
    {
        var run = await _forecastService.GetOrComputeAsync(request.HorizonMonths, cancellationToken);
        if (run.IsFailure)
            return Result.Failure<CashFlowForecastDto>(run.Error);

        var upcoming = await _repository.ListLinesAsync(
            new CashFlowLineQueryCriteria
            {
                ForecastRunId = run.Value.Id,
                Direction = CashFlowDirection.Inflow,
                Page = 1,
                PageSize = CashFlowForecastMappings.UpcomingInflowsCount
            },
            cancellationToken);

        var settings = await _settingsRepository.GetAsync(cancellationToken);

        return Result.Success(CashFlowForecastMappings.ToDto(run.Value, upcoming.Items, settings));
    }
}

/// <summary>
/// Flux attendus d'une projection, paginés et filtrables. Alimente la vue détaillée et l'export.
/// </summary>
public sealed record GetCashFlowLinesQuery(
    Guid RunId,
    string? Direction,
    string? SourceType,
    DateTime? From,
    DateTime? To,
    string? Search,
    int Page,
    int PageSize) : IRequest<Result<PagedResult<CashFlowLineDto>>>;

public sealed class GetCashFlowLinesQueryHandler
    : IRequestHandler<GetCashFlowLinesQuery, Result<PagedResult<CashFlowLineDto>>>
{
    private readonly ICashFlowForecastRepository _repository;

    public GetCashFlowLinesQueryHandler(ICashFlowForecastRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<PagedResult<CashFlowLineDto>>> Handle(
        GetCashFlowLinesQuery request,
        CancellationToken cancellationToken)
    {
        var run = await _repository.GetByIdAsync(request.RunId, cancellationToken);
        if (run is null)
            return Result.Failure<PagedResult<CashFlowLineDto>>(CashFlowForecastErrors.RunNotFound);

        var result = await _repository.ListLinesAsync(
            new CashFlowLineQueryCriteria
            {
                ForecastRunId = request.RunId,
                Direction = CashFlowForecastMappings.ParseOptionalDirection(request.Direction),
                SourceType = CashFlowForecastMappings.ParseSourceType(request.SourceType),
                ExpectedFrom = request.From,
                ExpectedTo = request.To,
                Search = request.Search,
                Page = request.Page,
                PageSize = request.PageSize
            },
            cancellationToken);

        return Result.Success(new PagedResult<CashFlowLineDto>
        {
            Items = result.Items.Select(CashFlowForecastMappings.ToDto).ToList(),
            Page = Math.Max(request.Page, 1),
            PageSize = Math.Clamp(request.PageSize, 1, 200),
            TotalCount = result.TotalCount
        });
    }
}

/// <summary>Engagements récurrents du tenant.</summary>
public sealed record GetRecurringCashCommitmentsQuery(bool IncludeInactive)
    : IRequest<Result<IReadOnlyList<RecurringCashCommitmentDto>>>;

public sealed class GetRecurringCashCommitmentsQueryHandler
    : IRequestHandler<GetRecurringCashCommitmentsQuery, Result<IReadOnlyList<RecurringCashCommitmentDto>>>
{
    private readonly IRecurringCashCommitmentRepository _repository;

    public GetRecurringCashCommitmentsQueryHandler(IRecurringCashCommitmentRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<IReadOnlyList<RecurringCashCommitmentDto>>> Handle(
        GetRecurringCashCommitmentsQuery request,
        CancellationToken cancellationToken)
    {
        var commitments = await _repository.ListAsync(request.IncludeInactive, cancellationToken);

        IReadOnlyList<RecurringCashCommitmentDto> dtos = commitments
            .Select(CashFlowForecastMappings.ToDto)
            .ToList();

        return Result.Success(dtos);
    }
}

/// <summary>Seuils de la jauge de position de trésorerie.</summary>
public sealed record GetCashFlowThresholdsQuery : IRequest<Result<CashFlowThresholdsDto>>;

public sealed class GetCashFlowThresholdsQueryHandler
    : IRequestHandler<GetCashFlowThresholdsQuery, Result<CashFlowThresholdsDto>>
{
    private readonly ICashFlowForecastSettingsRepository _repository;

    public GetCashFlowThresholdsQueryHandler(ICashFlowForecastSettingsRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<CashFlowThresholdsDto>> Handle(
        GetCashFlowThresholdsQuery request,
        CancellationToken cancellationToken)
    {
        var settings = await _repository.GetAsync(cancellationToken);
        return Result.Success(CashFlowForecastMappings.ToDto(settings));
    }
}
