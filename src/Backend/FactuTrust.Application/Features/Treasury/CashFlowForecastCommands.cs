using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.Common.Interfaces.Treasury;
using FactuTrust.Application.Configuration;
using FactuTrust.Application.Features.Treasury.Dtos;
using FactuTrust.Domain.Common;
using FactuTrust.Domain.Entities.Treasury;
using MediatR;
using Microsoft.Extensions.Options;

namespace FactuTrust.Application.Features.Treasury;

/// <summary>Recalcul explicite de la projection, déclenché par l'utilisateur.</summary>
public sealed record RecomputeCashFlowForecastCommand(int HorizonMonths, Guid UserId)
    : IRequest<Result<CashFlowForecastDto>>;

public sealed class RecomputeCashFlowForecastCommandHandler
    : IRequestHandler<RecomputeCashFlowForecastCommand, Result<CashFlowForecastDto>>
{
    private readonly ICashFlowForecastService _forecastService;
    private readonly ICashFlowForecastRepository _repository;
    private readonly ICashFlowForecastSettingsRepository _settingsRepository;
    private readonly TreasuryForecastOptions _options;
    private readonly TimeProvider _timeProvider;

    public RecomputeCashFlowForecastCommandHandler(
        ICashFlowForecastService forecastService,
        ICashFlowForecastRepository repository,
        ICashFlowForecastSettingsRepository settingsRepository,
        IOptions<TreasuryForecastOptions> options,
        TimeProvider timeProvider)
    {
        _forecastService = forecastService;
        _repository = repository;
        _settingsRepository = settingsRepository;
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public async Task<Result<CashFlowForecastDto>> Handle(
        RecomputeCashFlowForecastCommand request,
        CancellationToken cancellationToken)
    {
        // Un recalcul balaie l'ensemble des créances, dettes, cycles de paie et échéances : le
        // plafond journalier évite qu'un clic répété ne sature la base du tenant.
        var since = _timeProvider.GetUtcNow().UtcDateTime.AddDays(-1);
        var runsToday = await _repository.CountRunsSinceAsync(since, cancellationToken);

        if (runsToday >= Math.Max(1, _options.MaxRecomputeRunsPerDay))
            return Result.Failure<CashFlowForecastDto>(
                CashFlowForecastErrors.RateLimited(_options.MaxRecomputeRunsPerDay));

        var run = await _forecastService.RecomputeAsync(request.HorizonMonths, request.UserId, cancellationToken);
        if (run.IsFailure)
            return Result.Failure<CashFlowForecastDto>(run.Error);

        var upcoming = await _repository.ListLinesAsync(
            new CashFlowLineQueryCriteria
            {
                ForecastRunId = run.Value.Id,
                Direction = Domain.Enums.CashFlowDirection.Inflow,
                Page = 1,
                PageSize = CashFlowForecastMappings.UpcomingInflowsCount
            },
            cancellationToken);

        var settings = await _settingsRepository.GetAsync(cancellationToken);

        return Result.Success(CashFlowForecastMappings.ToDto(run.Value, upcoming.Items, settings));
    }
}

/// <summary>Création d'un engagement récurrent.</summary>
public sealed record CreateRecurringCashCommitmentCommand(SaveRecurringCashCommitmentRequest Request)
    : IRequest<Result<RecurringCashCommitmentDto>>;

public sealed class CreateRecurringCashCommitmentCommandHandler
    : IRequestHandler<CreateRecurringCashCommitmentCommand, Result<RecurringCashCommitmentDto>>
{
    private readonly IRecurringCashCommitmentRepository _repository;

    public CreateRecurringCashCommitmentCommandHandler(IRecurringCashCommitmentRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<RecurringCashCommitmentDto>> Handle(
        CreateRecurringCashCommitmentCommand request,
        CancellationToken cancellationToken)
    {
        var r = request.Request;

        var commitment = RecurringCashCommitment.Create(
            r.Label,
            CashFlowForecastMappings.ParseDirection(r.Direction),
            r.Amount,
            CashFlowForecastMappings.ParseFrequency(r.Frequency),
            r.DayOfMonth,
            r.StartDate,
            r.EndDate,
            r.Category,
            r.Notes);

        await _repository.AddAsync(commitment, cancellationToken);

        return Result.Success(CashFlowForecastMappings.ToDto(commitment));
    }
}

/// <summary>Mise à jour d'un engagement récurrent.</summary>
public sealed record UpdateRecurringCashCommitmentCommand(Guid Id, SaveRecurringCashCommitmentRequest Request)
    : IRequest<Result<RecurringCashCommitmentDto>>;

public sealed class UpdateRecurringCashCommitmentCommandHandler
    : IRequestHandler<UpdateRecurringCashCommitmentCommand, Result<RecurringCashCommitmentDto>>
{
    private readonly IRecurringCashCommitmentRepository _repository;

    public UpdateRecurringCashCommitmentCommandHandler(IRecurringCashCommitmentRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<RecurringCashCommitmentDto>> Handle(
        UpdateRecurringCashCommitmentCommand request,
        CancellationToken cancellationToken)
    {
        var commitment = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (commitment is null)
            return Result.Failure<RecurringCashCommitmentDto>(CashFlowForecastErrors.CommitmentNotFound);

        var r = request.Request;

        commitment.Update(
            r.Label,
            CashFlowForecastMappings.ParseDirection(r.Direction),
            r.Amount,
            CashFlowForecastMappings.ParseFrequency(r.Frequency),
            r.DayOfMonth,
            r.StartDate,
            r.EndDate,
            r.Category,
            r.Notes);

        await _repository.UpdateAsync(commitment, cancellationToken);

        return Result.Success(CashFlowForecastMappings.ToDto(commitment));
    }
}

/// <summary>
/// Désactivation d'un engagement récurrent.
/// </summary>
/// <remarks>
/// Désactivation et non suppression : les projections passées ont été calculées avec cet
/// engagement, et le supprimer rendrait leur <c>InputsJson</c> inexplicable.
/// </remarks>
public sealed record DeactivateRecurringCashCommitmentCommand(Guid Id) : IRequest<Result>;

public sealed class DeactivateRecurringCashCommitmentCommandHandler
    : IRequestHandler<DeactivateRecurringCashCommitmentCommand, Result>
{
    private readonly IRecurringCashCommitmentRepository _repository;

    public DeactivateRecurringCashCommitmentCommandHandler(IRecurringCashCommitmentRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result> Handle(
        DeactivateRecurringCashCommitmentCommand request,
        CancellationToken cancellationToken)
    {
        var commitment = await _repository.GetByIdAsync(request.Id, cancellationToken);
        if (commitment is null)
            return Result.Failure(CashFlowForecastErrors.CommitmentNotFound);

        commitment.Deactivate();
        await _repository.UpdateAsync(commitment, cancellationToken);

        return Result.Success();
    }
}

/// <summary>Enregistrement des seuils de la jauge.</summary>
public sealed record SaveCashFlowThresholdsCommand(SaveCashFlowThresholdsRequest Request)
    : IRequest<Result<CashFlowThresholdsDto>>;

public sealed class SaveCashFlowThresholdsCommandHandler
    : IRequestHandler<SaveCashFlowThresholdsCommand, Result<CashFlowThresholdsDto>>
{
    private readonly ICashFlowForecastSettingsRepository _repository;

    public SaveCashFlowThresholdsCommandHandler(ICashFlowForecastSettingsRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<CashFlowThresholdsDto>> Handle(
        SaveCashFlowThresholdsCommand request,
        CancellationToken cancellationToken)
    {
        var settings = await _repository.GetAsync(cancellationToken)
                       ?? CashFlowForecastSettings.CreateDefault(0m);

        var r = request.Request;
        settings.Update(r.CriticalThreshold, r.AlertThreshold, r.ComfortThreshold, r.PayrollPaymentDayOfMonth);

        await _repository.SaveAsync(settings, cancellationToken);

        return Result.Success(CashFlowForecastMappings.ToDto(settings));
    }
}
