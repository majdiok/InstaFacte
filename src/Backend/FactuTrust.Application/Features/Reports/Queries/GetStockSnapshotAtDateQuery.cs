using FactuTrust.Application.Common.Interfaces.Repositories;
using FactuTrust.Application.DTOs;
using FactuTrust.Application.Features.AI;
using FactuTrust.Domain.Common;
using MediatR;

namespace FactuTrust.Application.Features.Reports.Queries;

/// <summary>
/// Query to get stock snapshot at a given date (état de stock à une date antérieure).
/// </summary>
public sealed record GetStockSnapshotAtDateQuery(DateTime AsOfDate, Guid? WarehouseId)
    : IRequest<Result<IReadOnlyList<StockSnapshotRowDto>>>;

/// <summary>
/// Handler for GetStockSnapshotAtDateQuery.
/// </summary>
public sealed class GetStockSnapshotAtDateQueryHandler
    : IRequestHandler<GetStockSnapshotAtDateQuery, Result<IReadOnlyList<StockSnapshotRowDto>>>
{
    private readonly IStockMovementRepository _movementRepository;
    private readonly TimeProvider _timeProvider;

    public GetStockSnapshotAtDateQueryHandler(
        IStockMovementRepository movementRepository,
        TimeProvider timeProvider)
    {
        _movementRepository = movementRepository;
        _timeProvider = timeProvider;
    }

    public async Task<Result<IReadOnlyList<StockSnapshotRowDto>>> Handle(
        GetStockSnapshotAtDateQuery request,
        CancellationToken cancellationToken)
    {
        var asOfDate = request.AsOfDate.Date;
        // "Aujourd'hui" suit le calendrier Tunisie (Africa/Tunis, UTC+1), comme le reste de l'app.
        // Comparer à DateTime.UtcNow rejetait à tort "aujourd'hui" entre 23:00 et 00:00 UTC.
        var todayTunisia = ReportingPeriodResolver.GetTodayInTunisia(_timeProvider);
        if (DateOnly.FromDateTime(asOfDate) > todayTunisia)
            return Result.Failure<IReadOnlyList<StockSnapshotRowDto>>(
                Error.Validation("AsOfDate", "La date ne peut pas être dans le futur. Choisissez une date antérieure ou égale à aujourd'hui."));

        var asOfEndOfDayUtc = asOfDate.AddDays(1).AddTicks(-1);
        var rows = await _movementRepository.GetStockSnapshotAtDateAsync(
            asOfEndOfDayUtc,
            request.WarehouseId,
            cancellationToken);
        return Result.Success<IReadOnlyList<StockSnapshotRowDto>>(rows);
    }
}
